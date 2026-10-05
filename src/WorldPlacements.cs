namespace NivalisMods.Hoba;

// Authored world placements embedded in the DLL; no runtime placement editor is included.
internal static class WorldPlacements
{
    internal static ContainerPlacement[] Chests => new ContainerPlacement[]
    {
        new() { Id = "container-b2291ef5", Model = "Carboard_Box_Light_C_Open", Area = "18_Spire",
            Position = new float[] { -2.248059f, 185.39223f, 51.668896f },
            RotationEuler = new float[] { 0f, 356.94922f, 270.02927f },
            RotationQuaternion = new float[] { 0.018818388f, -0.018828f, -0.7066757f, 0.7070367f },
            MeshOffset = new float[] { 0.04579527f, 0.5775018f, -0.010190487f },
            Scale = 1f, InteractionRange = 1.5f },
        new() { Id = "container-0048b9a2", Model = "Carboard_Box_Light_C_Open", Area = "7_Sewers",
            Position = new float[] { 14.382411f, 18.922787f, -42.32307f },
            RotationEuler = new float[] { 0f, 89.15164f, 269.4075f },
            RotationQuaternion = new float[] { 0.4988441f, -0.49371198f, 0.50628555f, -0.5010769f },
            MeshOffset = new float[] { 0.04579527f, 0.5775018f, -0.010190487f },
            Scale = 1f, InteractionRange = 1.5f },
        new() { Id = "container-89a4d5d7", Model = "Carboard_Box_Light_C_Open", Area = "1_Lowtown",
            Position = new float[] { 24.548939f, -1.1226772f, -8.998339f },
            RotationEuler = new float[] { 0f, 158.63773f, 268.90598f },
            RotationQuaternion = new float[] { 0.7014575f, -0.6881899f, 0.1323026f, -0.12980019f },
            MeshOffset = new float[] { 0.04579527f, 0.5775018f, -0.010190487f },
            Scale = 0.929333f, InteractionRange = 1f },
    };
    internal static VendorLocationFile Vendors => new() { Locations = new[] { new VendorLocation {
        Id = "dealer-917cb99b", Name = "HOBA Dealer", Area = "9_Seaside_Boardwalk", NpcPrefab = "Random Female 05",
        X = -13.830869f, Y = 0.059776306f, Z = 38.800003f, Yaw = 32.94662f
    } } };
}
