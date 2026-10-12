using UnityEditor;
using UnityEngine;
using UnityEngine.SpatialTracking;
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
            GameObject ballGo = GameObject.Find("Volleyball");
            if (ballGo == null)
            {
                ballGo = new GameObject("Volleyball");
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
                Object.DestroyImmediate(visualBall.GetComponent<Collider>());

                // Blob Shadow Child
                GameObject blobShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                blobShadow.name = "BlobShadowDecal";
                blobShadow.transform.parent = ballGo.transform;
                blobShadow.transform.position = new Vector3(0f, 0.005f, -8.5f);
                blobShadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                blobShadow.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                Object.DestroyImmediate(blobShadow.GetComponent<Collider>());

                Undo.RegisterCreatedObjectUndo(ballGo, "Create Volleyball");
            }

            BallBody activeBallBody = ballGo.GetComponent<BallBody>();

            // 5. Match Coordinator
            GameObject coordinatorGo = GameObject.Find("MatchCoordinator");
            if (coordinatorGo == null)
            {
                coordinatorGo = new GameObject("MatchCoordinator");
                coordinatorGo.transform.parent = gameplayRoot.transform;
                coordinatorGo.AddComponent<MatchCoordinator>();
                Undo.RegisterCreatedObjectUndo(coordinatorGo, "Create Match Coordinator");
            }

            // 6. XR Origin (VR Rig)
            GameObject rigRoot = GetOrCreateRoot("XR Origin (VR Rig)");
            rigRoot.transform.position = new Vector3(0f, 0f, -5.0f);
            rigRoot.tag = "Player";

            XROrigin xrOrigin = rigRoot.GetComponent<XROrigin>();
            if (xrOrigin == null) xrOrigin = rigRoot.AddComponent<XROrigin>();

            GameObject offsetGo = GetOrCreateChild(rigRoot.transform, "Camera Offset");
            offsetGo.transform.localPosition = Vector3.zero;

            // Head / Main Camera
            GameObject camGo = GetOrCreateChild(offsetGo.transform, "Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200.0f;

            if (camGo.GetComponent<AudioListener>() == null)
            {
                camGo.AddComponent<AudioListener>();
            }

            TrackedPoseDriver headDriver = camGo.GetComponent<TrackedPoseDriver>();
            if (headDriver == null) headDriver = camGo.AddComponent<TrackedPoseDriver>();
            headDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Head);

            // Clean up old root standalone camera if present
            Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera c in allCams)
            {
                if (c != cam && c.transform.parent != offsetGo.transform)
                {
                    Object.DestroyImmediate(c.gameObject);
                }
            }

            // Left Controller
            GameObject leftHandGo = GetOrCreateChild(offsetGo.transform, "Left Hand Controller");
            leftHandGo.transform.localPosition = new Vector3(-0.25f, 1.0f, 0.35f);
            leftHandGo.tag = "GameController";
            TrackedPoseDriver leftDriver = leftHandGo.GetComponent<TrackedPoseDriver>();
            if (leftDriver == null) leftDriver = leftHandGo.AddComponent<TrackedPoseDriver>();
            leftDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.LeftPose);

            TrackedVelocityEstimator leftEstimator = leftHandGo.GetComponent<TrackedVelocityEstimator>();
            if (leftEstimator == null) leftEstimator = leftHandGo.AddComponent<TrackedVelocityEstimator>();
            CreateControllerVisuals(leftHandGo.transform, false);

            // Right Controller
            GameObject rightHandGo = GetOrCreateChild(offsetGo.transform, "Right Hand Controller");
            rightHandGo.transform.localPosition = new Vector3(0.25f, 1.0f, 0.35f);
            rightHandGo.tag = "GameController";
            TrackedPoseDriver rightDriver = rightHandGo.GetComponent<TrackedPoseDriver>();
            if (rightDriver == null) rightDriver = rightHandGo.AddComponent<TrackedPoseDriver>();
            rightDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.RightPose);

            TrackedVelocityEstimator rightEstimator = rightHandGo.GetComponent<TrackedVelocityEstimator>();
            if (rightEstimator == null) rightEstimator = rightHandGo.AddComponent<TrackedVelocityEstimator>();
            CreateControllerVisuals(rightHandGo.transform, true);

            // Configure XROrigin
            xrOrigin.Origin = rigRoot;
            xrOrigin.CameraFloorOffsetObject = offsetGo;
            xrOrigin.Camera = cam;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // Wire VR Volley Rig Components
            VRVolleyHaptics haptics = rigRoot.GetComponent<VRVolleyHaptics>();
            if (haptics == null) haptics = rigRoot.AddComponent<VRVolleyHaptics>();

            BodyContactVolumes contactVolumes = rigRoot.GetComponent<BodyContactVolumes>();
            if (contactVolumes == null) contactVolumes = rigRoot.AddComponent<BodyContactVolumes>();
            SerializedObject soVolumes = new SerializedObject(contactVolumes);
            soVolumes.FindProperty("_leftEstimator").objectReferenceValue = leftEstimator;
            soVolumes.FindProperty("_rightEstimator").objectReferenceValue = rightEstimator;
            soVolumes.ApplyModifiedProperties();

            HandBallContactSolver solver = rigRoot.GetComponent<HandBallContactSolver>();
            if (solver == null) solver = rigRoot.AddComponent<HandBallContactSolver>();
            SerializedObject soSolver = new SerializedObject(solver);
            soSolver.FindProperty("_ballBody").objectReferenceValue = activeBallBody;
            soSolver.FindProperty("_contactVolumes").objectReferenceValue = contactVolumes;
            soSolver.FindProperty("_haptics").objectReferenceValue = haptics;
            soSolver.ApplyModifiedProperties();

            // Locomotion (Smooth Thumbstick Movement & Snap Turn)
            VRPlayerLocomotion locomotion = rigRoot.GetComponent<VRPlayerLocomotion>();
            if (locomotion == null) locomotion = rigRoot.AddComponent<VRPlayerLocomotion>();

            // 7. Drill Situation Manager & 3D Interactive Kiosk
            DrillSituationManager drillManager = coordinatorGo.GetComponent<DrillSituationManager>();
            if (drillManager == null) drillManager = coordinatorGo.AddComponent<DrillSituationManager>();

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
            GameObject kioskGo = GameObject.Find("DrillKiosk_Menu");
            if (kioskGo != null)
            {
                Object.DestroyImmediate(kioskGo);
            }

            kioskGo = new GameObject("DrillKiosk_Menu");
            kioskGo.transform.parent = parent;
            // Positioned within direct arm's reach of the player spawn at (0, 0, -5.0)
            kioskGo.transform.position = new Vector3(-1.0f, 0f, -4.8f);
            kioskGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            // Kiosk Pedestal
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.parent = kioskGo.transform;
            pedestal.transform.localPosition = new Vector3(0f, 0.50f, 0f);
            pedestal.transform.localScale = new Vector3(0.14f, 0.50f, 0.14f);

            // Console Board (angled backwards 25 degrees)
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "ConsoleBoard";
            board.transform.parent = kioskGo.transform;
            board.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            board.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            board.transform.localScale = new Vector3(1.15f, 0.72f, 0.06f);

            // Header Title Text
            Create3DLabel(board.transform, "HeaderTitle", "VOLLEYBALL DRILL SELECTOR",
                new Vector3(0f, 0.28f, -0.04f), 0.013f, 40, new Color(1.0f, 0.85f, 0.20f));

            // Buttons
            CreateKioskButton(board.transform, drillManager, DrillSituation.DigFloatServe, false,
                "1. DIG FLOAT SERVE", new Vector3(-0.27f, 0.13f, -0.04f), new Color(0.20f, 0.55f, 0.95f));

            CreateKioskButton(board.transform, drillManager, DrillSituation.DigHardSpike, false,
                "2. DIG HARD SPIKE", new Vector3(0.27f, 0.13f, -0.04f), new Color(0.95f, 0.30f, 0.25f));

            CreateKioskButton(board.transform, drillManager, DrillSituation.SetterPractice, false,
                "3. SETTER DRILL", new Vector3(-0.27f, -0.03f, -0.04f), new Color(0.15f, 0.80f, 0.85f));

            CreateKioskButton(board.transform, drillManager, DrillSituation.SpikeApproachPractice, false,
                "4. SPIKE RUN-UP", new Vector3(0.27f, -0.03f, -0.04f), new Color(0.68f, 0.35f, 0.95f));

            CreateKioskButton(board.transform, drillManager, DrillSituation.SelfServe, false,
                "5. SELF SERVE (FLOAT)", new Vector3(-0.27f, -0.19f, -0.04f), new Color(0.20f, 0.82f, 0.45f));

            CreateKioskButton(board.transform, drillManager, DrillSituation.DigFloatServe, true,
                "REPLAY / REPEAT", new Vector3(0.27f, -0.19f, -0.04f), new Color(0.95f, 0.65f, 0.15f));

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
            btnGo.transform.localScale = new Vector3(0.50f, 0.12f, 0.04f);

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

            Create3DLabel(btnGo.transform, "Label", label, new Vector3(0f, 0f, -0.6f), 0.022f, 32, Color.white);
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

        private static void CreateControllerVisuals(Transform controllerTransform, bool isRight)
        {
            // Palm Visual
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            palm.name = isRight ? "RightPalmVisual" : "LeftPalmVisual";
            palm.transform.parent = controllerTransform;
            palm.transform.localPosition = new Vector3(0f, -0.02f, 0.06f);
            palm.transform.localScale = new Vector3(0.08f, 0.04f, 0.09f);
            Object.DestroyImmediate(palm.GetComponent<Collider>());

            // Forearm Visual
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arm.name = isRight ? "RightForearmVisual" : "LeftForearmVisual";
            arm.transform.parent = controllerTransform;
            arm.transform.localPosition = new Vector3(0f, -0.04f, -0.14f);
            arm.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            arm.transform.localScale = new Vector3(0.07f, 0.12f, 0.07f);
            Object.DestroyImmediate(arm.GetComponent<Collider>());

            // Button trigger volume
            BoxCollider trigger = controllerTransform.gameObject.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, -0.02f, 0.05f);
            trigger.size = new Vector3(0.12f, 0.08f, 0.16f);
            trigger.isTrigger = true;
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

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }
            GameObject go = new GameObject(name);
            go.transform.parent = parent;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }
    }
}
