using UnityEditor;
using UnityEngine;
using VRVolley.Gameplay;
using VRVolley.XR;

namespace VRVolley.Editor
{
    /// <summary>
    /// Editor tool that constructs an authentic FIVB Beach Volleyball court, net system,
    /// ball prefab with blob shadow, and player contact solver in the active scene.
    /// </summary>
    public static class CourtSceneBuilder
    {
        [MenuItem("VR Volley/Build Beach Volleyball Scene")]
        public static void BuildBeachCourtScene()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Beach Volleyball Scene");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Root Environment
            GameObject envRoot = GetOrCreateRoot("--- ENVIRONMENT ---");

            // Sand Court Ground
            GameObject sandGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sandGround.name = "SandGround";
            sandGround.transform.parent = envRoot.transform;
            sandGround.transform.position = Vector3.zero;
            sandGround.transform.localScale = new Vector3(3.0f, 1.0f, 3.0f); // 30m x 30m beach free zone
            sandGround.layer = LayerMask.NameToLayer("Default");
            Undo.RegisterCreatedObjectUndo(sandGround, "Create Sand Ground");

            // 2. Net System
            GameObject netRoot = new GameObject("NetSystem");
            netRoot.transform.parent = envRoot.transform;
            netRoot.transform.position = Vector3.zero;

            // Posts (X = -4.5m and +4.5m, Net height 2.43m)
            CreateNetPost(netRoot.transform, -4.5f);
            CreateNetPost(netRoot.transform, 4.5f);

            // Net Mesh (8.5m wide, 1m tall, centered at Y = 1.93m)
            GameObject netMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            netMesh.name = "NetMesh";
            netMesh.transform.parent = netRoot.transform;
            netMesh.transform.position = new Vector3(0f, 1.93f, 0f);
            netMesh.transform.localScale = new Vector3(8.5f, 1.0f, 0.04f);

            // Top Tape / Let Cord (Horizontal Cylinder along X at Y = 2.43m)
            GameObject topTape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            topTape.name = "NetTopTape_LetCord";
            topTape.transform.parent = netRoot.transform;
            topTape.transform.position = new Vector3(0f, 2.43f, 0f);
            topTape.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            topTape.transform.localScale = new Vector3(0.05f, 4.25f, 0.05f);

            // Antennas at X = -4.0m and X = +4.0m (FIVB: 1.80m above net tape)
            CreateAntenna(netRoot.transform, -4.0f);
            CreateAntenna(netRoot.transform, 4.0f);

            Undo.RegisterCreatedObjectUndo(netRoot, "Create Net System");

            // 3. Court Boundary Lines (16m x 8m)
            GameObject linesRoot = new GameObject("BoundaryLines_16x8m");
            linesRoot.transform.parent = envRoot.transform;
            linesRoot.transform.position = new Vector3(0f, 0.003f, 0f);

            CreateLineSegment(linesRoot.transform, "Sideline_Left", new Vector3(-4.0f, 0f, 0f), new Vector3(0.05f, 1f, 16.0f));
            CreateLineSegment(linesRoot.transform, "Sideline_Right", new Vector3(4.0f, 0f, 0f), new Vector3(0.05f, 1f, 16.0f));
            CreateLineSegment(linesRoot.transform, "Endline_TeamA", new Vector3(0f, 0f, -8.0f), new Vector3(8.0f, 1f, 0.05f));
            CreateLineSegment(linesRoot.transform, "Endline_TeamB", new Vector3(0f, 0f, 8.0f), new Vector3(8.0f, 1f, 0.05f));

            Undo.RegisterCreatedObjectUndo(linesRoot, "Create Boundary Lines");

            // 4. Volleyball GameObject
            GameObject gameplayRoot = GetOrCreateRoot("--- GAMEPLAY ---");
            GameObject ballGo = new GameObject("Volleyball");
            ballGo.transform.parent = gameplayRoot.transform;
            ballGo.transform.position = new Vector3(0f, 1.8f, -8.5f);

            Rigidbody rb = ballGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            SphereCollider sc = ballGo.AddComponent<SphereCollider>();
            sc.radius = 0.107f;
            sc.isTrigger = true;

            BallBody ballBody = ballGo.AddComponent<BallBody>();

            // Visual Ball Sphere Child
            GameObject visualBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualBall.name = "VisualBall";
            visualBall.transform.parent = ballGo.transform;
            visualBall.transform.localPosition = Vector3.zero;
            visualBall.transform.localScale = Vector3.one * (0.107f * 2f);
            Object.DestroyImmediate(visualBall.GetComponent<Collider>()); // Visual only

            // Blob Shadow Child
            GameObject blobShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blobShadow.name = "BlobShadowDecal";
            blobShadow.transform.parent = ballGo.transform;
            blobShadow.transform.position = new Vector3(0f, 0.005f, -8.5f);
            blobShadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            blobShadow.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            Object.DestroyImmediate(blobShadow.GetComponent<Collider>());

            Undo.RegisterCreatedObjectUndo(ballGo, "Create Volleyball");

            // 5. Match Coordinator
            GameObject coordinatorGo = new GameObject("MatchCoordinator");
            coordinatorGo.transform.parent = gameplayRoot.transform;
            MatchCoordinator coordinator = coordinatorGo.AddComponent<MatchCoordinator>();
            Undo.RegisterCreatedObjectUndo(coordinatorGo, "Create Match Coordinator");

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = ballGo;
            Debug.Log("[VR Volley] FIVB Beach Volleyball Court, Net, Volleyball, and Match Coordinator successfully created!");
        }

        private static void CreateNetPost(Transform parent, float xPos)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = xPos < 0 ? "NetPost_Left" : "NetPost_Right";
            post.transform.parent = parent;
            post.transform.position = new Vector3(xPos, 1.30f, 0f);
            post.transform.localScale = new Vector3(0.12f, 1.30f, 0.12f);
        }

        private static void CreateAntenna(Transform parent, float xPos)
        {
            GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.name = xPos < 0 ? "Antenna_Left" : "Antenna_Right";
            antenna.transform.parent = parent;
            antenna.transform.position = new Vector3(xPos, 2.43f + 0.90f, 0f); // Centers 1.80m antenna above net tape
            antenna.transform.localScale = new Vector3(0.02f, 0.90f, 0.02f);
            Collider col = antenna.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private static void CreateLineSegment(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = name;
            line.transform.parent = parent;
            line.transform.localPosition = pos;
            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.transform.localScale = scale;
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            }
            return go;
        }
    }
}
