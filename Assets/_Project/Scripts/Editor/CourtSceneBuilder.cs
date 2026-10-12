using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;
using VRVolley.Gameplay;
using VRVolley.XR;

namespace VRVolley.Editor
{
    /// <summary>
    /// Editor tool that constructs an authentic FIVB Beach Volleyball court, net system,
    /// ball with aerodynamic simulation, full XR Origin rig, and interactive 3D drill selector kiosk.
    /// </summary>
    public static class CourtSceneBuilder
    {
        [MenuItem("VR Volley/Build Beach Volleyball Scene")]
        public static void BuildBeachCourtScene()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Beach Volleyball Scene");
            int undoGroup = Undo.GetCurrentGroup();

            // 0. Clean up any existing generated roots to prevent duplicates
            DestroyIfExists("--- ENVIRONMENT ---");
            DestroyIfExists("--- GAMEPLAY ---");
            DestroyIfExists("XR Origin (VR Rig)");
            DestroyIfExists("DrillKiosk_Menu");

            // Clean up standalone cameras
            Camera[] oldCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera c in oldCams)
            {
                Object.DestroyImmediate(c.gameObject);
            }

            // 1. Root Environment
            GameObject envRoot = new GameObject("--- ENVIRONMENT ---");
            Undo.RegisterCreatedObjectUndo(envRoot, "Create Environment Root");

            // Sand Court Ground
            GameObject sandGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sandGround.name = "SandGround";
            sandGround.transform.parent = envRoot.transform;
            sandGround.transform.position = Vector3.zero;
            sandGround.transform.localScale = new Vector3(3.0f, 1.0f, 3.0f); // 30m x 30m beach free zone
            sandGround.layer = LayerMask.NameToLayer("Default");

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

            // 3. Court Boundary Lines (16m x 8m)
            GameObject linesRoot = new GameObject("BoundaryLines_16x8m");
            linesRoot.transform.parent = envRoot.transform;
            linesRoot.transform.position = new Vector3(0f, 0.003f, 0f);

            CreateLineSegment(linesRoot.transform, "Sideline_Left", new Vector3(-4.0f, 0f, 0f), new Vector3(0.05f, 1f, 16.0f));
            CreateLineSegment(linesRoot.transform, "Sideline_Right", new Vector3(4.0f, 0f, 0f), new Vector3(0.05f, 1f, 16.0f));
            CreateLineSegment(linesRoot.transform, "Endline_TeamA", new Vector3(0f, 0f, -8.0f), new Vector3(8.0f, 1f, 0.05f));
            CreateLineSegment(linesRoot.transform, "Endline_TeamB", new Vector3(0f, 0f, 8.0f), new Vector3(8.0f, 1f, 0.05f));

            // 4. Volleyball GameObject
            GameObject gameplayRoot = new GameObject("--- GAMEPLAY ---");
            Undo.RegisterCreatedObjectUndo(gameplayRoot, "Create Gameplay Root");

            GameObject ballGo = new GameObject("Volleyball");
            ballGo.transform.parent = gameplayRoot.transform;
            ballGo.transform.position = new Vector3(0f, 1.8f, -8.5f);

            Rigidbody rb = ballGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            SphereCollider sc = ballGo.AddComponent<SphereCollider>();
            sc.radius = 0.107f;
            sc.isTrigger = true;

            BallBody activeBallBody = ballGo.AddComponent<BallBody>();

            // Visual Ball Sphere Child
            GameObject visualBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualBall.name = "VisualBall";
            visualBall.transform.parent = ballGo.transform;
            visualBall.transform.localPosition = Vector3.zero;
            visualBall.transform.localScale = Vector3.one * (0.107f * 2f);
            Object.DestroyImmediate(visualBall.GetComponent<Collider>());

            // Blob Shadow Child
            GameObject blobShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blobShadow.name = "BlobShadowDecal";
            blobShadow.transform.parent = ballGo.transform;
            blobShadow.transform.position = new Vector3(0f, 0.005f, -8.5f);
            blobShadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            blobShadow.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            Object.DestroyImmediate(blobShadow.GetComponent<Collider>());

            // 5. Match Coordinator
            GameObject coordinatorGo = new GameObject("MatchCoordinator");
            coordinatorGo.transform.parent = gameplayRoot.transform;
            coordinatorGo.AddComponent<MatchCoordinator>();

            // 6. XR Origin (VR Rig)
            GameObject rigRoot = new GameObject("XR Origin (VR Rig)");
            rigRoot.transform.position = new Vector3(0f, 0f, -5.0f);
            rigRoot.tag = "Player";
            Undo.RegisterCreatedObjectUndo(rigRoot, "Create XR Origin");

            XROrigin xrOrigin = rigRoot.AddComponent<XROrigin>();

            GameObject offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.parent = rigRoot.transform;
            offsetGo.transform.localPosition = Vector3.zero;

            // Head / Main Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.transform.parent = offsetGo.transform;
            camGo.transform.localPosition = Vector3.zero;
            camGo.tag = "MainCamera";

            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200.0f;
            camGo.AddComponent<AudioListener>();

            // Head Tracking
            camGo.AddComponent<VRTrackedHead>();

            // Left Controller
            GameObject leftHandGo = new GameObject("Left Hand Controller");
            leftHandGo.transform.parent = offsetGo.transform;
            leftHandGo.transform.localPosition = new Vector3(-0.25f, 1.0f, 0.35f);
            leftHandGo.tag = "GameController";

            GameObject leftVisualRoot = CreateControllerVisuals(leftHandGo.transform, false);
            VRTrackedController leftTracker = leftHandGo.AddComponent<VRTrackedController>();
            leftTracker.Configure(false, leftVisualRoot);

            TrackedVelocityEstimator leftEstimator = leftHandGo.AddComponent<TrackedVelocityEstimator>();

            // Right Controller
            GameObject rightHandGo = new GameObject("Right Hand Controller");
            rightHandGo.transform.parent = offsetGo.transform;
            rightHandGo.transform.localPosition = new Vector3(0.25f, 1.0f, 0.35f);
            rightHandGo.tag = "GameController";

            GameObject rightVisualRoot = CreateControllerVisuals(rightHandGo.transform, true);
            VRTrackedController rightTracker = rightHandGo.AddComponent<VRTrackedController>();
            rightTracker.Configure(true, rightVisualRoot);

            TrackedVelocityEstimator rightEstimator = rightHandGo.AddComponent<TrackedVelocityEstimator>();

            // Configure XROrigin
            xrOrigin.Origin = rigRoot;
            xrOrigin.CameraFloorOffsetObject = offsetGo;
            xrOrigin.Camera = cam;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // Wire VR Volley Rig Components
            VRVolleyHaptics haptics = rigRoot.AddComponent<VRVolleyHaptics>();

            BodyContactVolumes contactVolumes = rigRoot.AddComponent<BodyContactVolumes>();
            SerializedObject soVolumes = new SerializedObject(contactVolumes);
            soVolumes.FindProperty("_leftEstimator").objectReferenceValue = leftEstimator;
            soVolumes.FindProperty("_rightEstimator").objectReferenceValue = rightEstimator;
            soVolumes.ApplyModifiedProperties();

            HandBallContactSolver solver = rigRoot.AddComponent<HandBallContactSolver>();
            SerializedObject soSolver = new SerializedObject(solver);
            soSolver.FindProperty("_ballBody").objectReferenceValue = activeBallBody;
            soSolver.FindProperty("_contactVolumes").objectReferenceValue = contactVolumes;
            soSolver.FindProperty("_haptics").objectReferenceValue = haptics;
            soSolver.ApplyModifiedProperties();

            // Smooth Locomotion & Snap Turn
            VRPlayerLocomotion locomotion = rigRoot.AddComponent<VRPlayerLocomotion>();

            // 7. Drill Situation Manager & 3D Interactive Kiosk
            DrillSituationManager drillManager = coordinatorGo.AddComponent<DrillSituationManager>();
            GameObject kioskGo = CreateDrillKiosk(gameplayRoot.transform, drillManager);

            SerializedObject soManager = new SerializedObject(drillManager);
            soManager.FindProperty("_ballBody").objectReferenceValue = activeBallBody;
            soManager.FindProperty("_playerRigTransform").objectReferenceValue = rigRoot.transform;
            soManager.FindProperty("_kioskTransform").objectReferenceValue = kioskGo.transform;
            soManager.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = ballGo;
            Debug.Log("[VR Volley] FIVB Beach Volleyball Court, XR Rig, and 3D Drill Kiosk successfully built!");
        }

        private static GameObject CreateDrillKiosk(Transform parent, DrillSituationManager drillManager)
        {
            GameObject kioskGo = new GameObject("DrillKiosk_Menu");
            kioskGo.transform.parent = parent;
            // Positioned cleanly within arm's reach of player spawn at (0, 0, -5.0)
            kioskGo.transform.position = new Vector3(-0.95f, 0f, -4.75f);
            kioskGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            // Kiosk Pedestal (Stops cleanly at Y = 0.84m so it NEVER pokes through the board!)
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.parent = kioskGo.transform;
            pedestal.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            pedestal.transform.localScale = new Vector3(0.10f, 0.42f, 0.10f);

            // Console Panel root (Uniform scale Vector3.one so all child text is 100% distortion-free!)
            GameObject panel = new GameObject("ConsolePanel");
            panel.transform.parent = kioskGo.transform;
            panel.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            panel.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            panel.transform.localScale = Vector3.one;

            // Flat Backboard Mesh
            GameObject boardMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boardMesh.name = "BackboardMesh";
            boardMesh.transform.parent = panel.transform;
            boardMesh.transform.localPosition = Vector3.zero;
            boardMesh.transform.localRotation = Quaternion.identity;
            boardMesh.transform.localScale = new Vector3(1.10f, 0.72f, 0.04f);

            // Header Title Text
            Create3DLabel(panel.transform, "HeaderTitle", "VOLLEYBALL DRILLS",
                new Vector3(0f, 0.28f, -0.035f), 0.016f, 44, new Color(1.0f, 0.85f, 0.15f));

            // Buttons: 2 columns x 3 rows
            CreateKioskButton(panel.transform, drillManager, DrillSituation.DigFloatServe, false,
                "1. DIG FLOAT SERVE", new Vector3(-0.27f, 0.13f, -0.035f), new Color(0.20f, 0.60f, 1.0f));

            CreateKioskButton(panel.transform, drillManager, DrillSituation.DigHardSpike, false,
                "2. DIG HARD SPIKE", new Vector3(0.27f, 0.13f, -0.035f), new Color(1.0f, 0.32f, 0.28f));

            CreateKioskButton(panel.transform, drillManager, DrillSituation.SetterPractice, false,
                "3. SETTER DRILL", new Vector3(-0.27f, -0.03f, -0.035f), new Color(0.20f, 0.88f, 0.90f));

            CreateKioskButton(panel.transform, drillManager, DrillSituation.SpikeApproachPractice, false,
                "4. SPIKE RUN-UP", new Vector3(0.27f, -0.03f, -0.035f), new Color(0.75f, 0.40f, 1.0f));

            CreateKioskButton(panel.transform, drillManager, DrillSituation.SelfServe, false,
                "5. SELF SERVE (FLOAT)", new Vector3(-0.27f, -0.19f, -0.035f), new Color(0.20f, 0.88f, 0.50f));

            CreateKioskButton(panel.transform, drillManager, DrillSituation.DigFloatServe, true,
                "REPLAY / REPEAT", new Vector3(0.27f, -0.19f, -0.035f), new Color(1.0f, 0.70f, 0.20f));

            return kioskGo;
        }

        private static void CreateKioskButton(
            Transform parent,
            DrillSituationManager manager,
            DrillSituation situation,
            bool isRepeat,
            string label,
            Vector3 localPos,
            Color color)
        {
            GameObject btnGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnGo.name = isRepeat ? "Button_Repeat" : "Button_" + situation;
            btnGo.transform.parent = parent;
            btnGo.transform.localPosition = localPos;
            btnGo.transform.localRotation = Quaternion.identity;
            btnGo.transform.localScale = new Vector3(0.48f, 0.12f, 0.04f);

            Collider col = btnGo.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            MeshRenderer renderer = btnGo.GetComponent<MeshRenderer>();
            DrillKioskButton btnComp = btnGo.AddComponent<DrillKioskButton>();
            if (isRepeat)
            {
                btnComp.ConfigureRepeat(manager, renderer, color);
            }
            else
            {
                btnComp.Configure(situation, manager, renderer, color);
            }

            Vector3 textPos = localPos + new Vector3(0f, 0f, -0.025f);
            Create3DLabel(parent, "Label_" + btnGo.name, label, textPos, 0.010f, 36, Color.white);
        }

        private static void Create3DLabel(
            Transform parent,
            string name,
            string text,
            Vector3 localPos,
            float charSize,
            int fontSize,
            Color textColor)
        {
            GameObject textGo = new GameObject(name);
            textGo.transform.parent = parent;
            textGo.transform.localPosition = localPos;
            textGo.transform.localRotation = Quaternion.identity;
            textGo.transform.localScale = Vector3.one;

            TextMesh textMesh = textGo.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = charSize;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = textColor;
        }

        private static GameObject CreateControllerVisuals(Transform controllerTransform, bool isRight)
        {
            GameObject visualRoot = new GameObject(isRight ? "RightVisualRoot" : "LeftVisualRoot");
            visualRoot.transform.parent = controllerTransform;
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            // Palm Visual
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            palm.name = isRight ? "RightPalmVisual" : "LeftPalmVisual";
            palm.transform.parent = visualRoot.transform;
            palm.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);
            palm.transform.localScale = new Vector3(0.08f, 0.04f, 0.09f);
            Object.DestroyImmediate(palm.GetComponent<Collider>());

            // Forearm Visual
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arm.name = isRight ? "RightForearmVisual" : "LeftForearmVisual";
            arm.transform.parent = visualRoot.transform;
            arm.transform.localPosition = new Vector3(0f, -0.04f, -0.14f);
            arm.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            arm.transform.localScale = new Vector3(0.07f, 0.12f, 0.07f);
            Object.DestroyImmediate(arm.GetComponent<Collider>());

            // Fingertip Pointer Sphere (gives clear visual target for touching kiosk buttons)
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = isRight ? "RightPointerTip" : "LeftPointerTip";
            tip.transform.parent = visualRoot.transform;
            tip.transform.localPosition = new Vector3(0f, -0.01f, 0.12f);
            tip.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
            Object.DestroyImmediate(tip.GetComponent<Collider>());

            // Trigger volume for kiosk button pressing
            BoxCollider trigger = controllerTransform.gameObject.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, -0.02f, 0.06f);
            trigger.size = new Vector3(0.14f, 0.10f, 0.18f);
            trigger.isTrigger = true;

            return visualRoot;
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
            antenna.transform.position = new Vector3(xPos, 2.43f + 0.90f, 0f);
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

        private static void DestroyIfExists(string name)
        {
            GameObject go = GameObject.Find(name);
            while (go != null)
            {
                Object.DestroyImmediate(go);
                go = GameObject.Find(name);
            }
        }
    }
}
