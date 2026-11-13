using UnityEngine;
using UnityEngine.AI;

namespace VRScienceLab.Module2.EditorTools
{
    // Laboratory assistant robot: a wheeled service robot that carries a tube rack between the
    // stations of the lab. The model is assembled from generated meshes; navigation is done by
    // LabAssistantRobot (NavMeshAgent) and the motion details by RobotVisualAnimator.
    public static partial class Module2SceneBuilder
    {
        private struct StationSpec
        {
            public string Name;
            public Vector3 Position;
            public float Yaw;

            public StationSpec(string name, float x, float z, float yaw)
            {
                Name = name;
                Position = new Vector3(x, 0f, z);
                Yaw = yaw;
            }
        }

        private static void BuildRobot(BuildContext context)
        {
            Palette palette = context.Palette;

            // Index 0 is the charging dock. Yaw is the direction the robot faces when parked.
            StationSpec[] specs =
            {
                new StationSpec("Charging Dock", 3.3f, -4.15f, 0f),
                new StationSpec("Main Workbench", -1.45f, -0.65f, 0f),
                new StationSpec("Sample Storage", -3.15f, -0.3f, -90f),
                new StationSpec("Analysis Desk", 2.55f, -2.0f, 90f),
                new StationSpec("Fume Hood", -2.85f, -3.35f, -90f),
            };

            Transform stationsRoot = new GameObject("Robot Stations").transform;
            var stations = new Object[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                GameObject station = CreateRoot(specs[i].Name, stationsRoot, specs[i].Position);
                station.transform.rotation = Quaternion.Euler(0f, specs[i].Yaw, 0f);

                GameObject marker = Lathe("Marker", station.transform, "StationMarker", RingProfile(0.34f, 0.3f, 0.003f), new Vector3(0f, 0.001f, 0f), palette.Marker, 48);
                NoShadows(marker);
                NoShadows(Box("Direction", station.transform, new Vector3(0f, 0.002f, 0.22f), new Vector3(0.05f, 0.003f, 0.12f), palette.Marker, false));

                var component = station.AddComponent<RobotStation>();
                SetString(component, "stationName", specs[i].Name);
                SetReference(component, "marker", marker.GetComponent<Renderer>());
                stations[i] = component;
            }

            // Charging dock on the rear wall behind station 0.
            Transform dock = CreateChild("Charging Dock Unit", stationsRoot, new Vector3(specs[0].Position.x, 0f, RoomMinZ + 0.07f)).transform;
            Bevel("Housing", dock, new Vector3(0f, 0.12f, 0f), new Vector3(0.56f, 0.62f, 0.12f), 0.03f, 0.008f, palette.BodyDark, true);
            NoShadows(Box("Charge Light", dock, new Vector3(0f, 0.62f, 0.062f), new Vector3(0.36f, 0.02f, 0.004f), palette.Accent, false));
            NoShadows(Box("Contact Plate", dock, new Vector3(0f, 0.3f, 0.062f), new Vector3(0.2f, 0.08f, 0.004f), palette.BrushedSteel, false));
            NoShadows(Lathe("Floor Pad", dock, "DockPad", CylinderProfile(0.42f, 0.004f, 0.001f), new Vector3(0f, 0f, specs[0].Position.z - (RoomMinZ + 0.07f)), palette.Rubber, 48));

            // ---------------- Robot ----------------
            GameObject robot = CreateRoot("Lab Assistant Robot", null, specs[0].Position);
            robot.transform.rotation = Quaternion.Euler(0f, specs[0].Yaw, 0f);

            // Chassis and wheels.
            Lathe("Chassis", robot.transform, "RobotChassis", new[]
            {
                new Vector2(0f, 0.045f), new Vector2(0.2f, 0.045f), new Vector2(0.245f, 0.085f), new Vector2(0.25f, 0.17f),
                new Vector2(0.23f, 0.205f), new Vector2(0.15f, 0.225f), new Vector2(0f, 0.225f),
            }, Vector3.zero, palette.RobotDark, 40);
            GameObject statusRing = Lathe("StatusRing", robot.transform, "RobotStatusRing", RingProfile(0.256f, 0.244f, 0.014f), new Vector3(0f, 0.12f, 0f), palette.RobotStatus, 48);
            NoShadows(statusRing);

            var wheels = new Object[2];
            for (int side = -1; side <= 1; side += 2)
            {
                Transform wheel = CreateChild(side < 0 ? "Wheel L" : "Wheel R", robot.transform, new Vector3(side * 0.225f, 0.07f, 0f)).transform;
                GameObject tyre = Lathe("Tyre", wheel, "RobotTyre", new[]
                {
                    new Vector2(0f, -0.02f), new Vector2(0.055f, -0.02f), new Vector2(0.07f, -0.012f), new Vector2(0.07f, 0.012f),
                    new Vector2(0.055f, 0.02f), new Vector2(0f, 0.02f),
                }, Vector3.zero, palette.Rubber, 28);
                tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                GameObject hub = Lathe("Hub", wheel, "RobotHub", CylinderProfile(0.04f, 0.006f, 0.002f), new Vector3(side * 0.02f, 0f, 0f), palette.Steel, 20);
                hub.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * side);
                // Off-centre mark so the wheel rotation is visible.
                Box("Spoke", wheel, new Vector3(side * 0.027f, 0.022f, 0f), new Vector3(0.003f, 0.03f, 0.012f), palette.RobotStatus, false);
                wheels[side < 0 ? 0 : 1] = wheel;
            }

            CreatePrimitive(PrimitiveType.Sphere, "Caster Front", robot.transform, new Vector3(0f, 0.03f, 0.17f), Vector3.one * 0.06f, palette.Rubber, false);
            CreatePrimitive(PrimitiveType.Sphere, "Caster Rear", robot.transform, new Vector3(0f, 0.03f, -0.17f), Vector3.one * 0.06f, palette.Rubber, false);

            // Body: everything above the chassis bobs and leans together.
            Transform body = CreateChild("Body", robot.transform, new Vector3(0f, 0.22f, 0f)).transform;
            Lathe("Torso", body, "RobotTorso", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.15f, 0f), new Vector2(0.175f, 0.06f), new Vector2(0.19f, 0.2f),
                new Vector2(0.18f, 0.36f), new Vector2(0.15f, 0.47f), new Vector2(0.1f, 0.54f), new Vector2(0.06f, 0.56f), new Vector2(0f, 0.56f),
            }, Vector3.zero, palette.RobotShell, 40);
            NoShadows(Lathe("Waist Band", body, "RobotWaistBand", RingProfile(0.181f, 0.17f, 0.012f), new Vector3(0f, 0.075f, 0f), palette.AccentDim, 40));
            Lathe("Neck", body, "RobotNeck", CylinderProfile(0.045f, 0.05f, 0.004f), new Vector3(0f, 0.55f, 0f), palette.RobotDark, 20);

            // Chest screen (faces forward, +Z).
            Bevel("Chest Screen", body, new Vector3(0f, 0.25f, 0.176f), new Vector3(0.2f, 0.1f, 0.02f), 0.008f, 0.003f, palette.RobotVisor);
            TMPro.TextMeshPro chestText = CreateText("Chest Text", body, new Vector3(0f, 0.3f, 0.1875f), new Vector3(0f, 180f, 0f), "READY", 0.2f, DisplayTextColor, new Vector2(0.18f, 0.06f), TMPro.FontStyles.Bold);

            // Arms holding a carrying tray with a tube rack.
            for (int side = -1; side <= 1; side += 2)
            {
                CreatePrimitive(PrimitiveType.Sphere, side < 0 ? "Shoulder L" : "Shoulder R", body, new Vector3(side * 0.185f, 0.36f, 0.01f), Vector3.one * 0.085f, palette.RobotDark, false);
                MeshObject(side < 0 ? "Arm L" : "Arm R", body, ProceduralMeshFactory.Tube(side < 0 ? "RobotArmL" : "RobotArmR", ProceduralMeshFactory.SmoothPath(new[]
                {
                    new Vector3(side * 0.195f, 0.36f, 0.01f), new Vector3(side * 0.215f, 0.26f, 0.05f),
                    new Vector3(side * 0.2f, 0.18f, 0.15f), new Vector3(side * 0.15f, 0.165f, 0.245f),
                }), 0.024f, 12), Vector3.zero, palette.RobotShell);
                CreatePrimitive(PrimitiveType.Sphere, side < 0 ? "Gripper L" : "Gripper R", body, new Vector3(side * 0.15f, 0.165f, 0.25f), Vector3.one * 0.058f, palette.RobotDark, false);
            }

            Transform tray = CreateChild("Carry Tray", body, new Vector3(0f, 0.185f, 0.285f)).transform;
            Bevel("Tray", tray, Vector3.zero, new Vector3(0.34f, 0.012f, 0.17f), 0.02f, 0.003f, palette.BrushedSteel);
            Bevel("Tray Lip", tray, new Vector3(0f, 0.012f, 0.079f), new Vector3(0.33f, 0.012f, 0.008f), 0.003f, 0.001f, palette.BrushedSteel);
            BuildTubeRack(palette, CreateChild("Tube Rack", tray, new Vector3(0f, 0.012f, 0.005f)).transform, 0f);

            // Head: turns toward the destination.
            Transform head = CreateChild("Head", body, new Vector3(0f, 0.6f, 0f)).transform;
            Bevel("Skull", head, Vector3.zero, new Vector3(0.27f, 0.17f, 0.21f), 0.07f, 0.03f, palette.RobotShell);
            Bevel("Visor", head, new Vector3(0f, 0.045f, 0.094f), new Vector3(0.22f, 0.085f, 0.022f), 0.01f, 0.004f, palette.RobotVisor);
            var eyes = new Renderer[2];
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = Lathe(side < 0 ? "Eye L" : "Eye R", head, "RobotEye", CylinderProfile(0.017f, 0.004f, 0.001f), new Vector3(side * 0.05f, 0.088f, 0.1165f), palette.RobotStatus, 20);
                eye.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                NoShadows(eye);
                eyes[side < 0 ? 0 : 1] = eye.GetComponent<Renderer>();

                GameObject ear = Lathe(side < 0 ? "Ear L" : "Ear R", head, "RobotEar", CylinderProfile(0.038f, 0.014f, 0.004f), new Vector3(side * 0.135f, 0.085f, 0f), palette.RobotDark, 20);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * side);
            }

            Lathe("Antenna", head, "RobotAntenna", CylinderProfile(0.004f, 0.09f, 0.001f), new Vector3(0.07f, 0.168f, -0.02f), palette.Steel, 8);
            GameObject antennaTip = CreatePrimitive(PrimitiveType.Sphere, "Antenna Tip", head, new Vector3(0.07f, 0.262f, -0.02f), Vector3.one * 0.022f, palette.RobotStatus, false);
            NoShadows(antennaTip);

            // Physics presence: kinematic body so loose objects and the cart collide with the robot.
            var capsule = robot.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.52f, 0f);
            capsule.radius = 0.26f;
            capsule.height = 1.04f;
            var rigidbody = robot.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var agent = robot.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.1f;
            agent.baseOffset = 0f;
            agent.speed = 0.9f;
            agent.angularSpeed = 170f;
            agent.acceleration = 1.8f;
            agent.stoppingDistance = 0.06f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            var assistant = robot.AddComponent<LabAssistantRobot>();
            SetReferences(assistant, "stations", stations);

            var animator = robot.AddComponent<RobotVisualAnimator>();
            SetReference(animator, "robot", assistant);
            SetReferences(animator, "wheels", wheels);
            SetReference(animator, "body", body);
            SetReference(animator, "head", head);
            SetReferences(animator, "statusRenderers", statusRing.GetComponent<Renderer>(), eyes[0], eyes[1], antennaTip.GetComponent<Renderer>());
            SetReference(animator, "statusText", chestText);

            context.Robot = assistant;
        }
    }
}
