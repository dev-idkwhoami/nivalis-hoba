using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nivalis;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

internal sealed class SprayCanModel : IDisposable
{
    private readonly List<Mesh> _meshes = new();
    private readonly List<Material> _materials = new();
    private readonly List<Transform> _droplets = new();
    private readonly GameObject _root, _grip;
    private readonly Transform _eye;
    private readonly IKSolverLimb _solver;
    private readonly Transform? _oldTarget;
    private readonly Vector3 _oldPosition;
    private readonly Quaternion _oldRotation, _handFrame;
    private readonly float _oldPositionWeight, _oldRotationWeight;
    private readonly List<(Transform Bone, Quaternion Original, Quaternion Grip)> _fingers = new();
    private bool _visible, _spraying;
    private float _sprayProgress;
    private Vector3? _sprayDestination;
    private readonly Transform _hand, _forearm;
    private readonly List<(Transform Bone, Vector3 Position, Quaternion Rotation)> _armPose = new();
    private readonly Transform _upperArm;
    private readonly List<Quaternion> _armOffsets = new();
    private bool _attached;

    private readonly List<(Transform[] Bones, Vector3 Tip, Vector3 Contact)> _contacts = new();
    private static readonly Vector3 Rest = new(.22f, -.27f, .43f);

    internal SprayCanModel(PlayerCharacter player, BoardProduct paint)
    {
        var hands = player.Controller.HandsAnimator;
        _eye = player.Controller.Camera.transform;
        _solver = hands.rightHandIK?.solver ?? throw new InvalidOperationException("Right-hand IK is not available.");
        _oldTarget = _solver.target;
        _oldPosition = _solver.IKPosition; _oldRotation = _solver.IKRotation;
        _oldPositionWeight = _solver.IKPositionWeight; _oldRotationWeight = _solver.IKRotationWeight;
        if (_oldTarget != null && _oldPositionWeight > .05f)
            throw new InvalidOperationException("The right hand is already occupied by another interaction.");
        var animator = hands.Animator;
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand == null || middle == null || index == null || little == null)
            throw new InvalidOperationException("Player hand bones are not available.");
        _hand = hand;
        _upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm)
            ?? throw new InvalidOperationException("Player upper arm bone is not available.");
        _forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        if (_forearm == null) throw new InvalidOperationException("Player forearm bone is not available.");
        var normal = -Vector3.Cross(index.position-hand.position, little.position-hand.position).normalized;
        _handFrame = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(middle.position-hand.position, normal);
        _root = new GameObject("HOBA held spray can");
        _grip = new GameObject("HOBA spray hand target");
        try
        {
            _root.transform.SetParent(_eye, false);
            // Camera rigs can inherit a scaled player root. Keep the prop in world metres.
            var inherited = _eye.lossyScale;
            _root.transform.localScale = new Vector3(.4f / inherited.x, .4f / inherited.y, .4f / inherited.z);
            _grip.transform.SetParent(_eye, false);
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color") ?? throw new InvalidOperationException("No spray shader.");
            var design = BoardDesigns.All.First(d => d.Family == paint.Family && !d.Legendary);
            var palette = BoardDesigns.Palette(design, paint.Finish);
            foreach (var group in PaintGeometry.Create().GroupBy(p => p.Finish))
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                foreach (var part in group)
                {
                    var offset = vertices.Count;
                    vertices.AddRange(part.Vertices.Select(v => new Vector3(v.X,v.Y,v.Z)));
                    triangles.AddRange(part.Triangles.Select(i => i+offset));
                }
                var mesh = new Mesh { name = "Spray can " + group.Key };
                _meshes.Add(mesh);
                mesh.vertices = new Il2CppStructArray<Vector3>(vertices.ToArray());
                mesh.triangles = new Il2CppStructArray<int>(triangles.ToArray());
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var rgb = palette[group.Key];
                var material = new Material(shader) { color = new Color(rgb.X,rgb.Y,rgb.Z) };
                _materials.Add(material);
                var obj = new GameObject(group.Key);
                obj.transform.SetParent(_root.transform,false);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            // Reusable little voxel droplets, no colliders or transient per-frame objects.
            var cube = BoardGeometry.Surface(new List<BoardPart> { BoardGeometry.Box("Drop", "shell", System.Numerics.Vector3.Zero, new(.02f,.02f,.02f),0) })[0];
            var dropMesh = new Mesh { name = "HOBA spray droplets" }; _meshes.Add(dropMesh);
            dropMesh.vertices = new Il2CppStructArray<Vector3>(cube.Vertices.Select(v => new Vector3(v.X,v.Y,v.Z)).ToArray());
            dropMesh.triangles = new Il2CppStructArray<int>(cube.Triangles.ToArray());
            dropMesh.RecalculateNormals(); dropMesh.RecalculateBounds();
            var tint = palette["yellow"];
            var dropMaterial = new Material(Shader.Find("Unlit/Color") ?? shader) { color = new Color(tint.X,tint.Y,tint.Z) }; _materials.Add(dropMaterial);
            for (var i=0;i<48;i++)
            {
                var drop = new GameObject("Paint mist " + i);
                drop.transform.SetParent(_root.transform,false);
                drop.AddComponent<MeshFilter>().sharedMesh = dropMesh;
                var renderer = drop.AddComponent<MeshRenderer>(); renderer.sharedMaterial = dropMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                drop.SetActive(false); _droplets.Add(drop.transform);
            }
            foreach (var names in new[] {
                new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
                new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
                new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
                new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
                new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal } })
            {
            var bones = names.Select(animator.GetBoneTransform).ToArray();
            if (bones.Any(b => b == null)) continue;
            var tipDirection = (bones[2].position - bones[1].position).normalized;
            var tip = bones[2].InverseTransformPoint(bones[2].position + tipDirection * .014f);
            var contact = names[0] switch
            {
                HumanBodyBones.RightIndexProximal => new Vector3(0, .23f, 0),
                HumanBodyBones.RightThumbProximal => new Vector3(-.06f, .045f, -.015f),
                HumanBodyBones.RightMiddleProximal => new Vector3(.065f, .105f, -.02f),
                HumanBodyBones.RightRingProximal => new Vector3(.07f, .02f, -.015f),
                _ => new Vector3(.06f, -.055f, -.02f)
            };
            _contacts.Add((bones, tip, contact));
            for (var i=0;i<names.Length;i++)
            {
                var bone = animator.GetBoneTransform(names[i]);
                if (bone == null) continue;
                var next = i<2 ? animator.GetBoneTransform(names[i+1]) : null;
                var direction = next != null ? next.position-bone.position : bone.position-bone.parent.position;
                var axis = Quaternion.Inverse(bone.rotation) * Vector3.Cross(direction.normalized,normal).normalized;
                var angle = names[0] == HumanBodyBones.RightIndexProximal ? (i == 0 ? 15f : 20f) :
                    names[0] == HumanBodyBones.RightThumbProximal ? 10f : i == 0 ? 30f : 40f;
                _fingers.Add((bone,bone.localRotation,bone.localRotation*Quaternion.AngleAxis(angle,axis)));
            }
            }
            SetVisible(true);
            Animate(false,0,null);
        }
        catch { Dispose(); throw; }
    }

    internal void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        _root.SetActive(visible);
        if (!visible) ReleasePose();
    }
    internal void Animate(bool spraying, float progress, Vector3? destination)
    {
        // Capture input in Update; position the prop and mist together in LateUpdate.
        _spraying = spraying;
        _sprayProgress = progress;
        _sprayDestination = destination;
    }
    private void AnimateMist()
    {
        var spraying = _spraying;
        var progress = _sprayProgress;
        var destination = _sprayDestination;
        for (var i=0;i<_droplets.Count;i++)
        {
            var drop = _droplets[i];
            drop.gameObject.SetActive(spraying && destination.HasValue);
            if (!spraying || !destination.HasValue) continue;
            var t = Mathf.Repeat(progress*3+i/(float)_droplets.Count,1);
            var nozzle = _root.transform.TransformPoint(new Vector3(.03f,.21f,0));
            var spread = new Vector3(Mathf.Sin(i*2.3f),Mathf.Cos(i*3.1f),Mathf.Sin(i*4.7f)) * (.002f+.075f*t);
            drop.position = Vector3.Lerp(nozzle,destination.Value,t) + spread;
            drop.localScale = Vector3.one*(.35f+.8f*t);
        }
    }
    internal void PoseHand()
    {
        if (!_visible || _eye == null) return;
        // ReleasePose restores last frame's unmodified bones before the Animator runs.
        // Keep the native animation, adding only a fixed holding-pose rotation offset.
        _armPose.Clear();
        SaveArmPose(_upperArm); SaveArmPose(_forearm); SaveArmPose(_hand);
        if (!_attached)
        {
            // Calibrate the grip once. After this block the can is permanently hand-local;
            // neither Update nor LateUpdate positions or stabilizes it against the camera.
            _root.transform.SetParent(_eye, false);
            var inherited = _eye.lossyScale;
            _root.transform.localScale = new Vector3(.4f / inherited.x, .4f / inherited.y, .4f / inherited.z);
            _root.transform.localPosition = Rest;
            _root.transform.localRotation = Quaternion.Euler(-12, -90, -10);
            _grip.transform.position = _root.transform.position + _eye.right * .065f - _eye.up * .055f - _eye.forward * .025f;
            _solver.target = _grip.transform;
            _solver.IKPositionWeight = 1;
            _solver.IKRotationWeight = 0;
            _solver.Update();
            var forward = (_hand.position - _forearm.position).normalized;
            var palm = Vector3.ProjectOnPlane(-_eye.right, forward).normalized;
            _grip.transform.rotation = Quaternion.LookRotation(forward, palm) * Quaternion.Inverse(_handFrame);
            _solver.IKRotationWeight = 1;
            _solver.Update();
            foreach (var pose in _armPose)
                _armOffsets.Add(Quaternion.Inverse(pose.Rotation) * pose.Bone.localRotation);
            _root.transform.SetParent(_hand, true);
            _attached = true;
            // No ongoing IK target pulling the wrist back toward the camera.
            RestoreHand();
        }
        else
        {
            for (var i = 0; i < _armPose.Count; i++)
                _armPose[i].Bone.localRotation = _armPose[i].Rotation * _armOffsets[i];
        }
        // Spray sweep moves the wrist and can as a single unit.
        if (_spraying)
            _hand.localRotation *= Quaternion.Euler(0, Mathf.Sin(_sprayProgress * Mathf.PI * 2) * 7, 0);
        foreach (var finger in _fingers) if (finger.Bone != null) finger.Bone.localRotation = finger.Grip;
        // Separate contacts wrap three fingers around the far side and oppose the thumb
        // on the near side. Only the index reaches for the nozzle; it never drags the wrist.
        foreach (var contact in _contacts)
        {
            var target = _root.transform.TransformPoint(contact.Contact);
            for (var pass = 0; pass < 5; pass++)
            for (var joint = 2; joint >= 0; joint--)
            {
                var bone = contact.Bones[joint];
                var tip = contact.Bones[2].TransformPoint(contact.Tip);
                var rotation = Quaternion.FromToRotation(tip - bone.position, target - bone.position) * bone.rotation;
                bone.rotation = Quaternion.RotateTowards(bone.rotation, rotation, 18);
                var rest = _fingers.First(f => f.Bone == bone).Original;
                bone.localRotation = Quaternion.RotateTowards(rest, bone.localRotation, joint == 0 ? 65 : 85);
            }
        }
        AnimateMist();
    }
    private void SaveArmPose(Transform bone) => _armPose.Add((bone, bone.localPosition, bone.localRotation));

    internal void ReleasePose()
    {
        // The can stays attached while the wrist is restored and animated.
        foreach (var pose in _armPose)
        {
            if (pose.Bone == null) continue;
            pose.Bone.localPosition = pose.Position;
            pose.Bone.localRotation = pose.Rotation;
        }
        _armPose.Clear();
        RestoreHand();
    }
    private void RestoreHand()
    {
        foreach (var finger in _fingers) if (finger.Bone != null) finger.Bone.localRotation = finger.Original;
        _solver.target = _oldTarget;
        _solver.IKPosition = _oldPosition; _solver.IKRotation = _oldRotation;
        _solver.IKPositionWeight = _oldPositionWeight; _solver.IKRotationWeight = _oldRotationWeight;
    }
    public void Dispose()
    {
        try { ReleasePose(); }
        finally
        {
            if (_root != null) { _root.SetActive(false); Object.Destroy(_root); }
            if (_grip != null) Object.Destroy(_grip);
            foreach (var mesh in _meshes) Object.Destroy(mesh);
            foreach (var material in _materials) Object.Destroy(material);
            _meshes.Clear(); _materials.Clear();
        }
    }
}
