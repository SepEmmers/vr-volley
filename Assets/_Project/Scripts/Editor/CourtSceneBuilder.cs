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

            // 2. Net System (Authentic See-Through FIVB Mesh, Tapes & Antennas)
            CreateNetSystem(envRoot.transform);

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
            SerializedObject soBall = new SerializedObject(activeBallBody);
            soBall.FindProperty("_environmentLayers").intValue = LayerMask.GetMask("Default");
            soBall.ApplyModifiedProperties();

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

            Rigidbody leftRb = leftHandGo.AddComponent<Rigidbody>();
            leftRb.isKinematic = true;
            leftRb.useGravity = false;

            CreateControllerVisuals(leftHandGo.transform, false, out GameObject leftReceivingVisual, out GameObject leftPointerTip, out GameObject leftClaspedVisual);
            VRTrackedController leftTracker = leftHandGo.AddComponent<VRTrackedController>();

            TrackedVelocityEstimator leftEstimator = leftHandGo.AddComponent<TrackedVelocityEstimator>();

            // Right Controller
            GameObject rightHandGo = new GameObject("Right Hand Controller");
            rightHandGo.transform.parent = offsetGo.transform;
            rightHandGo.transform.localPosition = new Vector3(0.25f, 1.0f, 0.35f);
            rightHandGo.tag = "GameController";

            Rigidbody rightRb = rightHandGo.AddComponent<Rigidbody>();
            rightRb.isKinematic = true;
            rightRb.useGravity = false;

            CreateControllerVisuals(rightHandGo.transform, true, out GameObject rightReceivingVisual, out GameObject rightPointerTip, out GameObject rightClaspedVisual);
            VRTrackedController rightTracker = rightHandGo.AddComponent<VRTrackedController>();

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

            leftTracker.Configure(false, leftReceivingVisual, leftPointerTip, leftClaspedVisual, contactVolumes);
            rightTracker.Configure(true, rightReceivingVisual, rightPointerTip, rightClaspedVisual, contactVolumes);

            HandBallContactSolver solver = rigRoot.AddComponent<HandBallContactSolver>();
            SerializedObject soSolver = new SerializedObject(solver);
            soSolver.FindProperty("_ballBody").objectReferenceValue = activeBallBody;
            soSolver.FindProperty("_contactVolumes").objectReferenceValue = contactVolumes;
            soSolver.FindProperty("_haptics").objectReferenceValue = haptics;
            soSolver.ApplyModifiedProperties();

            // Smooth Locomotion, Snap Turn & Athletic Arm-Swing Jumping
            VRPlayerLocomotion locomotion = rigRoot.AddComponent<VRPlayerLocomotion>();
            locomotion.ConfigureEstimators(leftEstimator, rightEstimator, haptics);

            // Hand & Controller Angle Calibration Tool (Kept for future settings menu)
            HandCalibrationTool calibrationTool = rigRoot.AddComponent<HandCalibrationTool>();
            SerializedObject soCalib = new SerializedObject(calibrationTool);
            soCalib.FindProperty("_leftTracker").objectReferenceValue = leftTracker;
            soCalib.FindProperty("_rightTracker").objectReferenceValue = rightTracker;
            soCalib.FindProperty("_haptics").objectReferenceValue = haptics;
            soCalib.ApplyModifiedProperties();

            // Receive Bounciness & Energy Calibration Tool
            ReceiveBounceCalibrationTool bounceTool = rigRoot.AddComponent<ReceiveBounceCalibrationTool>();

            // 7. Drill Situation Manager & 3D Interactive Kiosk
            DrillSituationManager drillManager = coordinatorGo.AddComponent<DrillSituationManager>();
            GameObject kioskGo = CreateDrillKiosk(gameplayRoot.transform, drillManager, bounceTool);

            // 8. Ball Grab & Toss for Serving
            BallTossGrabbable tossGrabbable = ballGo.AddComponent<BallTossGrabbable>();
            tossGrabbable.Configure(
                activeBallBody,
                drillManager,
                haptics,
                leftEstimator,
                rightEstimator,
                leftTracker,
                rightTracker,
                solver);

            SerializedObject soBounce = new SerializedObject(bounceTool);
            soBounce.FindProperty("_solver").objectReferenceValue = solver;
            soBounce.FindProperty("_haptics").objectReferenceValue = haptics;
            soBounce.FindProperty("_tossGrabbable").objectReferenceValue = tossGrabbable;
            soBounce.FindProperty("_locomotion").objectReferenceValue = locomotion;
            soBounce.ApplyModifiedProperties();

            SerializedObject soManager = new SerializedObject(drillManager);
            soManager.FindProperty("_ballBody").objectReferenceValue = activeBallBody;
            soManager.FindProperty("_playerRigTransform").objectReferenceValue = rigRoot.transform;
            soManager.FindProperty("_kioskTransform").objectReferenceValue = kioskGo.transform;
            soManager.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = ballGo;
            Debug.Log("[VR Volley] FIVB Beach Volleyball Court, XR Rig, and 3D Drill Kiosk successfully built!");
        }

        private static GameObject CreateDrillKiosk(Transform parent, DrillSituationManager drillManager, ReceiveBounceCalibrationTool bounceTool)
        {
            GameObject kioskGo = new GameObject("DrillKiosk_Menu");
            kioskGo.transform.parent = parent;
            // Positioned cleanly outside the left sideline (court sideline is at X = -4.0m)
            kioskGo.transform.position = new Vector3(-4.60f, 0f, -4.50f);
            kioskGo.transform.rotation = Quaternion.Euler(0f, 75f, 0f);

            // Kiosk Pedestal (Stops cleanly at Y = 0.70m so it NEVER pokes through the board!)
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.parent = kioskGo.transform;
            pedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            pedestal.transform.localScale = new Vector3(0.10f, 0.35f, 0.10f);

            // Console Panel root (Uniform scale Vector3.one so all child text is 100% distortion-free!)
            GameObject panel = new GameObject("ConsolePanel");
            panel.transform.parent = kioskGo.transform;
            panel.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            panel.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            panel.transform.localScale = Vector3.one;

            // Flat Backboard Mesh (taller 1.25m height to cleanly house all setting controls)
            GameObject boardMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boardMesh.name = "BackboardMesh";
            boardMesh.transform.parent = panel.transform;
            boardMesh.transform.localPosition = Vector3.zero;
            boardMesh.transform.localRotation = Quaternion.identity;
            boardMesh.transform.localScale = new Vector3(1.10f, 1.25f, 0.04f);

            KioskTabController tabCtrl = panel.AddComponent<KioskTabController>();

            // Tab 1 & Tab 2 Navigation Header
            Vector3 tabBtnScale = new Vector3(0.46f, 0.085f, 0.04f);
            MeshRenderer tabDrillsMr = CreateTabButton(panel.transform, KioskTab.Drills, tabCtrl,
                "VOLLEYBALL DRILLS", new Vector3(-0.25f, 0.52f, -0.035f), tabBtnScale);

            MeshRenderer tabCalibMr = CreateTabButton(panel.transform, KioskTab.Calibration, tabCtrl,
                "SETTINGS / CALIB", new Vector3(0.25f, 0.52f, -0.035f), tabBtnScale);

            // PAGE 1: DRILLS
            GameObject drillsPage = new GameObject("Page_Drills");
            drillsPage.transform.parent = panel.transform;
            drillsPage.transform.localPosition = Vector3.zero;
            drillsPage.transform.localRotation = Quaternion.identity;
            drillsPage.transform.localScale = Vector3.one;

            Create3DLabel(drillsPage.transform, "DrillsSubHeader", "--- SELECT DRILL SCENARIO ---",
                new Vector3(0f, 0.40f, -0.035f), 0.011f, 30, new Color(1.0f, 0.85f, 0.20f));

            CreateKioskButton(drillsPage.transform, drillManager, DrillSituation.DigFloatServe, false,
                "1. DIG FLOAT SERVE", new Vector3(-0.27f, 0.26f, -0.035f), new Color(0.20f, 0.60f, 1.0f));

            CreateKioskButton(drillsPage.transform, drillManager, DrillSituation.DigHardSpike, false,
                "2. DIG HARD SPIKE", new Vector3(0.27f, 0.26f, -0.035f), new Color(1.0f, 0.32f, 0.28f));

            CreateKioskButton(drillsPage.transform, drillManager, DrillSituation.SetterPractice, false,
                "3. SETTER DRILL", new Vector3(-0.27f, 0.10f, -0.035f), new Color(0.20f, 0.88f, 0.90f));

            CreateKioskButton(drillsPage.transform, drillManager, DrillSituation.SpikeApproachPractice, false,
                "4. SPIKE RUN-UP", new Vector3(0.27f, 0.10f, -0.035f), new Color(0.75f, 0.40f, 1.0f));

            CreateKioskButton(drillsPage.transform, drillManager, DrillSituation.SelfServe, false,
                "5. SELF SERVE (FLOAT)", new Vector3(-0.27f, -0.06f, -0.035f), new Color(0.20f, 0.88f, 0.50f));

            CreateKioskButton(drillsPage.transform, drillManager, DrillSituation.DigFloatServe, true,
                "REPLAY / REPEAT", new Vector3(0.27f, -0.06f, -0.035f), new Color(1.0f, 0.70f, 0.20f));

            Create3DLabel(drillsPage.transform, "DrillsHint", "(Touch Button with Finger & Pull Trigger to Start)",
                new Vector3(0f, -0.25f, -0.035f), 0.008f, 24, new Color(0.75f, 0.75f, 0.75f));

            // PAGE 2: CALIBRATION & SETTINGS
            GameObject calibPage = new GameObject("Page_Calibration");
            calibPage.transform.parent = panel.transform;
            calibPage.transform.localPosition = Vector3.zero;
            calibPage.transform.localRotation = Quaternion.identity;
            calibPage.transform.localScale = Vector3.one;

            Create3DLabel(calibPage.transform, "CalibTitle", "--- SETTINGS & CALIBRATION ---",
                new Vector3(0f, 0.44f, -0.035f), 0.010f, 28, new Color(0.35f, 0.85f, 1.0f));

            GameObject readoutGo = Create3DLabel(calibPage.transform, "BounceReadout",
                "BOUNCE: 100% (Crisp FIVB Pop) | TOSS: Stabilized\nSERVE POWER: 2.0x Amplification (Default 2.0x)\nJUMP THRESHOLD: 1.7 m/s (Lower = Easier, Default 1.7)",
                new Vector3(0f, 0.33f, -0.035f), 0.0075f, 24, Color.white);
            TextMesh readoutTm = readoutGo.GetComponent<TextMesh>();
            if (bounceTool != null)
            {
                bounceTool.SetReadoutTextMesh(readoutTm);
            }

            // 1. Receive Bounce Section
            Create3DLabel(calibPage.transform, "Sec1_Title", "--- 1. RECEIVE BOUNCE PLATFORM ---",
                new Vector3(0f, 0.23f, -0.035f), 0.0075f, 22, new Color(0.40f, 0.90f, 0.80f));

            Vector3 row1BtnScale = new Vector3(0.18f, 0.070f, 0.04f);
            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.BounceMinus10,
                "-10%", new Vector3(-0.40f, 0.15f, -0.035f), row1BtnScale, new Color(0.20f, 0.70f, 0.85f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.BounceMinus1,
                "-1%", new Vector3(-0.20f, 0.15f, -0.035f), row1BtnScale, new Color(0.20f, 0.80f, 0.75f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.ResetDefault,
                "RESET 100%", new Vector3(0.00f, 0.15f, -0.035f), row1BtnScale, new Color(0.85f, 0.50f, 0.20f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.BouncePlus1,
                "+1%", new Vector3(0.20f, 0.15f, -0.035f), row1BtnScale, new Color(0.20f, 0.80f, 0.75f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.BouncePlus10,
                "+10%", new Vector3(0.40f, 0.15f, -0.035f), row1BtnScale, new Color(0.20f, 0.70f, 0.85f));

            Vector3 row2BtnScale = new Vector3(0.22f, 0.070f, 0.04f);
            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.PresetSoft,
                "SOFT (75%)", new Vector3(-0.36f, 0.06f, -0.035f), row2BtnScale, new Color(0.35f, 0.60f, 0.85f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.PresetCrisp,
                "CRISP (100%)", new Vector3(-0.12f, 0.06f, -0.035f), row2BtnScale, new Color(0.25f, 0.80f, 0.50f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.PresetMaxPop,
                "MAX POP (125%)", new Vector3(0.12f, 0.06f, -0.035f), row2BtnScale, new Color(0.90f, 0.40f, 0.70f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.ToggleTossMode,
                "TOSS MODE", new Vector3(0.36f, 0.06f, -0.035f), row2BtnScale, new Color(0.95f, 0.55f, 0.15f));

            // 2. Serve Power Section
            Create3DLabel(calibPage.transform, "Sec2_Title", "--- 2. SERVE POWER AMPLIFICATION ---",
                new Vector3(0f, -0.04f, -0.035f), 0.0075f, 22, new Color(1.0f, 0.65f, 0.20f));

            Vector3 row3BtnScale = new Vector3(0.28f, 0.075f, 0.04f);
            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.SpikePowerMinus,
                "-0.1x POWER", new Vector3(-0.30f, -0.13f, -0.035f), row3BtnScale, new Color(0.85f, 0.45f, 0.20f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.SpikePowerReset,
                "RESET 2.0x", new Vector3(0.00f, -0.13f, -0.035f), row3BtnScale, new Color(1.0f, 0.75f, 0.20f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.SpikePowerPlus,
                "+0.1x POWER", new Vector3(0.30f, -0.13f, -0.035f), row3BtnScale, new Color(0.85f, 0.45f, 0.20f));

            // 3. Jump Swing Sensitivity Section
            Create3DLabel(calibPage.transform, "Sec3_Title", "--- 3. JUMP ARM-SWING SENSITIVITY ---",
                new Vector3(0f, -0.23f, -0.035f), 0.0075f, 22, new Color(0.70f, 0.50f, 1.0f));

            Vector3 row4BtnScale = new Vector3(0.28f, 0.075f, 0.04f);
            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.JumpSensitivityMinus,
                "-0.1 (EASIER)", new Vector3(-0.30f, -0.32f, -0.035f), row4BtnScale, new Color(0.30f, 0.70f, 0.85f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.JumpSensitivityReset,
                "RESET 1.7 m/s", new Vector3(0.00f, -0.32f, -0.035f), row4BtnScale, new Color(0.70f, 0.40f, 0.95f));

            CreateBounceButton(calibPage.transform, bounceTool, ReceiveBounceAction.JumpSensitivityPlus,
                "+0.1 (HARDER)", new Vector3(0.30f, -0.32f, -0.035f), row4BtnScale, new Color(0.30f, 0.70f, 0.85f));

            Create3DLabel(calibPage.transform, "CalibHint", "(Touch Button with Finger & Pull Trigger to Adjust)",
                new Vector3(0f, -0.44f, -0.035f), 0.008f, 24, new Color(0.75f, 0.75f, 0.75f));

            tabCtrl.Configure(drillsPage, calibPage, tabDrillsMr, tabCalibMr);
            tabCtrl.SetTab(KioskTab.Drills);

            return kioskGo;
        }

        private static MeshRenderer CreateTabButton(
            Transform parent,
            KioskTab tab,
            KioskTabController controller,
            string label,
            Vector3 localPos,
            Vector3 scale)
        {
            GameObject btnGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnGo.name = "TabButton_" + tab;
            btnGo.transform.parent = parent;
            btnGo.transform.localPosition = localPos;
            btnGo.transform.localRotation = Quaternion.identity;
            btnGo.transform.localScale = scale;

            Collider col = btnGo.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            MeshRenderer renderer = btnGo.GetComponent<MeshRenderer>();
            KioskTabButton btnComp = btnGo.AddComponent<KioskTabButton>();
            btnComp.Configure(tab, controller, renderer);

            Vector3 textPos = localPos + new Vector3(0f, 0f, -0.025f);
            Create3DLabel(parent, "Label_" + btnGo.name, label, textPos, 0.010f, 32, Color.white);
            return renderer;
        }

        private static void CreateBounceButton(
            Transform parent,
            ReceiveBounceCalibrationTool tool,
            ReceiveBounceAction action,
            string label,
            Vector3 localPos,
            Vector3 scale,
            Color color)
        {
            GameObject btnGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnGo.name = "BounceButton_" + action;
            btnGo.transform.parent = parent;
            btnGo.transform.localPosition = localPos;
            btnGo.transform.localRotation = Quaternion.identity;
            btnGo.transform.localScale = scale;

            Collider col = btnGo.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            MeshRenderer renderer = btnGo.GetComponent<MeshRenderer>();
            ReceiveBounceButton btnComp = btnGo.AddComponent<ReceiveBounceButton>();
            btnComp.Configure(action, tool, renderer, color);

            Vector3 textPos = localPos + new Vector3(0f, 0f, -0.025f);
            Create3DLabel(parent, "Label_" + btnGo.name, label, textPos, 0.008f, 28, Color.white);
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
            btnGo.transform.localScale = new Vector3(0.48f, 0.10f, 0.04f);

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

        private static GameObject Create3DLabel(
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
            return textGo;
        }

        private static void CreateControllerVisuals(
            Transform controllerTransform, bool isRight,
            out GameObject receivingVisualRoot, out GameObject pointerTip, out GameObject claspedVisual)
        {
            GameObject visualRoot = new GameObject(isRight ? "RightVisualRoot" : "LeftVisualRoot");
            visualRoot.transform.parent = controllerTransform;
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            // Full Anatomical Low-Poly Athletic Volleyball Hand (always visible when controller is tracked)
            CreateAthleticHand(visualRoot.transform, isRight);

            // Receiving Forearm Visual (shown ONLY when gripping to receive)
            receivingVisualRoot = new GameObject(isRight ? "RightReceivingArm" : "LeftReceivingArm");
            receivingVisualRoot.transform.parent = visualRoot.transform;
            receivingVisualRoot.transform.localPosition = Vector3.zero;
            receivingVisualRoot.transform.localRotation = Quaternion.identity;

            CreateAthleticForearm(receivingVisualRoot.transform, isRight);

            // Dual 2-hand mode is enforced; eliminate synthetic clasp ghost visual
            claspedVisual = null;
            receivingVisualRoot.SetActive(false); // Initially hidden until grip is pressed!

            // Fingertip Pointer Sphere (aligned with index fingertip for kiosk selection)
            pointerTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointerTip.name = isRight ? "RightPointerTip" : "LeftPointerTip";
            pointerTip.transform.parent = visualRoot.transform;
            pointerTip.transform.localPosition = new Vector3(0f, -0.01f, 0.12f);
            pointerTip.transform.localScale = new Vector3(0.020f, 0.020f, 0.020f);
            Object.DestroyImmediate(pointerTip.GetComponent<Collider>());

            // Trigger volume for kiosk button pressing
            BoxCollider trigger = controllerTransform.gameObject.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, -0.02f, 0.06f);
            trigger.size = new Vector3(0.14f, 0.10f, 0.18f);
            trigger.isTrigger = true;
        }

        private static void CreateAthleticHand(Transform parent, bool isRight)
        {
            GameObject handRoot = new GameObject(isRight ? "RightAthleticHand" : "LeftAthleticHand");
            handRoot.transform.parent = parent;
            handRoot.transform.localPosition = Vector3.zero;
            handRoot.transform.localRotation = Quaternion.identity;

            // Lateral axis: side = isRight ? -1f : 1f correctly places the thumb medial (pointing inward toward torso)
            // and pinky lateral (pointing outward) on both hands.
            float side = isRight ? -1f : 1f;

            // 1. Palm base: flattened cube with contoured proportions
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "AthleticPalm";
            palm.transform.parent = handRoot.transform;
            palm.transform.localPosition = new Vector3(0f, -0.018f, 0.045f);
            palm.transform.localScale = new Vector3(0.078f, 0.024f, 0.065f);
            Object.DestroyImmediate(palm.GetComponent<Collider>());

            // 2. Thumb: angled inward and forward
            GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            thumb.name = "AthleticThumb";
            thumb.transform.parent = handRoot.transform;
            thumb.transform.localPosition = new Vector3(side * -0.038f, -0.016f, 0.030f);
            thumb.transform.localRotation = Quaternion.Euler(15f, side * -35f, side * -20f);
            thumb.transform.localScale = new Vector3(0.018f, 0.024f, 0.018f);
            Object.DestroyImmediate(thumb.GetComponent<Collider>());

            // 3. Four athletic cupped fingers
            // Index Finger
            GameObject indexF = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            indexF.name = "IndexFinger";
            indexF.transform.parent = handRoot.transform;
            indexF.transform.localPosition = new Vector3(side * -0.025f, -0.015f, 0.088f);
            indexF.transform.localRotation = Quaternion.Euler(10f, side * -4f, 0f);
            indexF.transform.localScale = new Vector3(0.015f, 0.028f, 0.015f);
            Object.DestroyImmediate(indexF.GetComponent<Collider>());

            // Middle Finger
            GameObject midF = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            midF.name = "MiddleFinger";
            midF.transform.parent = handRoot.transform;
            midF.transform.localPosition = new Vector3(side * -0.008f, -0.014f, 0.092f);
            midF.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            midF.transform.localScale = new Vector3(0.015f, 0.030f, 0.015f);
            Object.DestroyImmediate(midF.GetComponent<Collider>());

            // Ring Finger
            GameObject ringF = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ringF.name = "RingFinger";
            ringF.transform.parent = handRoot.transform;
            ringF.transform.localPosition = new Vector3(side * 0.009f, -0.015f, 0.088f);
            ringF.transform.localRotation = Quaternion.Euler(12f, side * 4f, 0f);
            ringF.transform.localScale = new Vector3(0.014f, 0.027f, 0.014f);
            Object.DestroyImmediate(ringF.GetComponent<Collider>());

            // Pinky Finger
            GameObject pinkyF = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pinkyF.name = "PinkyFinger";
            pinkyF.transform.parent = handRoot.transform;
            pinkyF.transform.localPosition = new Vector3(side * 0.026f, -0.017f, 0.082f);
            pinkyF.transform.localRotation = Quaternion.Euler(14f, side * 8f, 0f);
            pinkyF.transform.localScale = new Vector3(0.013f, 0.024f, 0.013f);
            Object.DestroyImmediate(pinkyF.GetComponent<Collider>());
        }

        private static void CreateAthleticForearm(Transform parent, bool isRight)
        {
            // Wrist Band
            GameObject wristBand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wristBand.name = isRight ? "RightWristBand" : "LeftWristBand";
            wristBand.transform.parent = parent;
            wristBand.transform.localPosition = new Vector3(0f, -0.025f, -0.015f);
            wristBand.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wristBand.transform.localScale = new Vector3(0.068f, 0.015f, 0.068f);
            Object.DestroyImmediate(wristBand.GetComponent<Collider>());

            // Forearm Cylinder (forms bump platform when gripping)
            GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.name = isRight ? "RightAthleticForearm" : "LeftAthleticForearm";
            forearm.transform.parent = parent;
            forearm.transform.localPosition = new Vector3(0f, -0.035f, -0.14f);
            forearm.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            forearm.transform.localScale = new Vector3(0.065f, 0.12f, 0.065f);
            Object.DestroyImmediate(forearm.GetComponent<Collider>());
        }

        private static void CreateNetSystem(Transform parent)
        {
            GameObject netRoot = new GameObject("NetSystem");
            netRoot.transform.parent = parent;
            netRoot.transform.position = Vector3.zero;

            // Posts (X = -4.5m and +4.5m, Net height 2.43m)
            CreateNetPost(netRoot.transform, -4.5f);
            CreateNetPost(netRoot.transform, 4.5f);

            Material netMat = GetOrCreateNetMaterial();
            Material tapeMat = GetOrCreateWhiteTapeMaterial();

            // Authentic See-Through Net Quads (8.5m wide, 1m tall, centered at Y = 1.93m)
            // Back-to-back quads ensure 100% two-sided visibility from both court halves
            GameObject netQuadFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
            netQuadFront.name = "NetMesh_GridFront";
            netQuadFront.transform.parent = netRoot.transform;
            netQuadFront.transform.position = new Vector3(0f, 1.93f, -0.001f);
            netQuadFront.transform.localScale = new Vector3(8.5f, 1.0f, 1.0f);
            MeshRenderer mrFront = netQuadFront.GetComponent<MeshRenderer>();
            if (mrFront != null && netMat != null) mrFront.sharedMaterial = netMat;
            Object.DestroyImmediate(netQuadFront.GetComponent<Collider>());

            GameObject netQuadBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
            netQuadBack.name = "NetMesh_GridBack";
            netQuadBack.transform.parent = netRoot.transform;
            netQuadBack.transform.position = new Vector3(0f, 1.93f, 0.001f);
            netQuadBack.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            netQuadBack.transform.localScale = new Vector3(8.5f, 1.0f, 1.0f);
            MeshRenderer mrBack = netQuadBack.GetComponent<MeshRenderer>();
            if (mrBack != null && netMat != null) mrBack.sharedMaterial = netMat;
            Object.DestroyImmediate(netQuadBack.GetComponent<Collider>());

            // Physical BoxCollider for Net Contacts
            GameObject netColliderGo = new GameObject("NetCollider");
            netColliderGo.transform.parent = netRoot.transform;
            netColliderGo.transform.position = new Vector3(0f, 1.93f, 0f);
            BoxCollider col = netColliderGo.AddComponent<BoxCollider>();
            col.size = new Vector3(8.5f, 1.0f, 0.04f);

            // White Top Tape (7 cm canvas band along Y = 2.43m)
            GameObject topTape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topTape.name = "NetTopTape_White";
            topTape.transform.parent = netRoot.transform;
            topTape.transform.position = new Vector3(0f, 2.43f, 0f);
            topTape.transform.localScale = new Vector3(8.5f, 0.07f, 0.02f);
            MeshRenderer topMr = topTape.GetComponent<MeshRenderer>();
            if (topMr != null && tapeMat != null) topMr.sharedMaterial = tapeMat;
            Object.DestroyImmediate(topTape.GetComponent<Collider>());

            // White Bottom Tape (5 cm canvas band along Y = 1.43m)
            GameObject bottomTape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomTape.name = "NetBottomTape_White";
            bottomTape.transform.parent = netRoot.transform;
            bottomTape.transform.position = new Vector3(0f, 1.43f, 0f);
            bottomTape.transform.localScale = new Vector3(8.5f, 0.05f, 0.02f);
            MeshRenderer btmMr = bottomTape.GetComponent<MeshRenderer>();
            if (btmMr != null && tapeMat != null) btmMr.sharedMaterial = tapeMat;
            Object.DestroyImmediate(bottomTape.GetComponent<Collider>());

            // Vertical Side Marker Bands (5 cm wide) directly under antennas at X = -4.0m and +4.0m
            CreateSideTape(netRoot.transform, -4.0f, tapeMat);
            CreateSideTape(netRoot.transform, 4.0f, tapeMat);

            // Antennas at X = -4.0m and X = +4.0m (FIVB: 1.80m above net tape)
            CreateAntenna(netRoot.transform, -4.0f);
            CreateAntenna(netRoot.transform, 4.0f);
        }

        private static void CreateSideTape(Transform parent, float xPos, Material mat)
        {
            GameObject sideTape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideTape.name = xPos < 0 ? "SideTape_Left" : "SideTape_Right";
            sideTape.transform.parent = parent;
            sideTape.transform.position = new Vector3(xPos, 1.93f, 0f);
            sideTape.transform.localScale = new Vector3(0.05f, 1.0f, 0.025f);
            MeshRenderer mr = sideTape.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
            Object.DestroyImmediate(sideTape.GetComponent<Collider>());
        }

        private static Material GetOrCreateNetMaterial()
        {
            string texDir = "Assets/_Project/Textures";
            string matDir = "Assets/_Project/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }
            if (!AssetDatabase.IsValidFolder(texDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Textures");
            }
            if (!AssetDatabase.IsValidFolder(matDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            string texPath = $"{texDir}/T_VolleyballNetGrid.png";
            string matPath = $"{matDir}/M_VolleyballNet.mat";

            Texture2D netTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (netTex == null)
            {
                int size = 64;
                netTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color32 cordColor = new Color32(24, 24, 24, 255); // Dark charcoal net cord
                Color32 clearColor = new Color32(0, 0, 0, 0);       // See-through hole
                Color32[] pixels = new Color32[size * size];

                int borderWidth = 4;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool isBorder = (x < borderWidth || x >= size - borderWidth ||
                                         y < borderWidth || y >= size - borderWidth);
                        pixels[y * size + x] = isBorder ? cordColor : clearColor;
                    }
                }

                netTex.SetPixels32(pixels);
                netTex.Apply();

                byte[] pngBytes = netTex.EncodeToPNG();
                System.IO.File.WriteAllBytes(texPath, pngBytes);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                netTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent Cutout");
                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", netTex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", netTex);

                // 85 squares horizontally (8.5m / 0.10m = 85), 10 squares vertically (1.0m / 0.10m = 10)
                if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", new Vector2(85f, 10f));
                if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", new Vector2(85f, 10f));

                // Alpha Cutout settings (Quest friendly depth-tested cutout)
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;

                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
            }

            return mat;
        }

        private static Material GetOrCreateWhiteTapeMaterial()
        {
            string matDir = "Assets/_Project/Materials";
            if (!AssetDatabase.IsValidFolder(matDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }
            string matPath = $"{matDir}/M_NetBoundaryTape.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
            }
            return mat;
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
