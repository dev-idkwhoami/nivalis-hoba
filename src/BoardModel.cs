using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

internal sealed class BoardModel : IDisposable
{
    internal GameObject Root { get; }
    internal BoardAudio Audio { get; }
    private readonly List<Mesh> _meshes = new();
    private readonly List<Material> _materials = new();
    private readonly List<(MeshRenderer Renderer, Color Color)> _glowing = new();
    private readonly MaterialPropertyBlock _pulse = new();
    private readonly Light _light;
    private GameObject? _scan;
    private BoardCompass? _compass;
    private readonly Dictionary<string, (Transform Transform, Vector3 Pivot)> _nodes = new();
    private readonly List<(Transform Transform, float X, string From, string To, List<CurvedFieldSurface> Surfaces)> _links = new();
    private readonly string _family;
    private readonly Dictionary<string, Color> _colors;
    private float _steeringTurn;
    private readonly List<(float Time, Vector3 Position, Quaternion Rotation, Vector3 Velocity, float TurnRate)> _motion = new();
    private readonly List<(Transform Transform, MeshRenderer Renderer, int Level)> _rings = new();

    internal BoardModel(string designId = BoardDesigns.DefaultId, string finishId = "neutral")
    {
        var design = BoardDesigns.Get(designId);
        var palette = BoardDesigns.Palette(design, finishId);
        _colors = palette.ToDictionary(p => p.Key, p => new Color(p.Value.X, p.Value.Y, p.Value.Z));
        _family = design.Family;
        Root = new GameObject(design.Name);
        Audio = new BoardAudio(Root);
        Object.DontDestroyOnLoad(Root);
        try
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color")
                ?? throw new InvalidOperationException("HOBA cannot find a supported board shader.");
            var parts = BoardVariants.Create(design);
            string Node(BoardPart part)
            {
                if (_family == "hoba") return "static";
                if (part.Name.StartsWith("Spine ")) return "spine-" + part.Name.Split(' ')[1];
                var z = part.Vertices.Average(v => v.Z);
                return z > .12f ? "front" : z < -.12f ? "rear" : "static";
            }
            bool IsLink(BoardPart part) => part.Name == "Energy tether" || part.Name == "Spine energy axis" || part.Name.StartsWith("Twin field beam");
            foreach (var node in parts.Where(p => !IsLink(p)).GroupBy(Node))
            {
                var pivot = node.Key == "front" ? new Vector3(0,0,.405f) : node.Key == "rear" ? new Vector3(0,0,-.405f) :
                    node.Key.StartsWith("spine-") ? new Vector3(0,.015f,node.SelectMany(p => p.Vertices).Average(v => v.Z)) : Vector3.zero;
                var body = new GameObject(node.Key);
                body.transform.SetParent(Root.transform, false);
                body.transform.localPosition = pivot;
                _nodes[node.Key] = (body.transform, pivot);
            }
            foreach (var group in parts.Where(p => !IsLink(p)).GroupBy(p => (Node: Node(p), Finish: p.Finish)))
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                foreach (var part in group)
                {
                    var offset = vertices.Count;
                    vertices.AddRange(part.Vertices.Select(p => new Vector3(p.X, p.Y, p.Z) - _nodes[group.Key.Node].Pivot));
                    triangles.AddRange(part.Triangles.Select(i => i + offset));
                }
                var mesh = new Mesh { name = design.Id + "." + group.Key.Node + "." + group.Key.Finish, indexFormat = IndexFormat.UInt32 };
                _meshes.Add(mesh);
                mesh.vertices = new Il2CppStructArray<Vector3>(vertices.ToArray());
                mesh.triangles = new Il2CppStructArray<int>(triangles.ToArray());
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var rgb = palette[group.Key.Finish];
                var color = new Color(rgb.X, rgb.Y, rgb.Z, 1);
                var material = new Material(shader) { name = mesh.name, color = color };
                _materials.Add(material);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", group.Key.Finish == "grip" ? .05f : .28f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", group.Key.Finish == "edge" ? .55f : .15f);
                var child = new GameObject(group.Key.Finish);
                child.transform.SetParent(_nodes[group.Key.Node].Transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                if (group.Key.Finish is "cyan" or "core")
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 4);
                    _glowing.Add((renderer, color));
                }
            }
            if (_family != "hoba") CreateLinks(shader, design.Legendary);
            // Shared open square-ring mesh matches the board's block-built silhouette.
            var ringPart = new BoardPart("Exhaust ring", "core");
            const float outer = .055f, inner = .038f;
            var corners = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
            for (var i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                System.Numerics.Vector3 V(Vector3 v) => new(v.x, v.y, v.z);
                ringPart.Quad(V(a * outer), V(b * outer), V(b * inner), V(a * inner));
                ringPart.Quad(V(a * inner), V(b * inner), V(b * outer), V(a * outer));
            }
            var ringMesh = new Mesh { name = "HOBA exhaust rings" };
            _meshes.Add(ringMesh);
            ringMesh.vertices = new Il2CppStructArray<Vector3>(ringPart.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray());
            ringMesh.triangles = new Il2CppStructArray<int>(ringPart.Triangles.ToArray());
            ringMesh.RecalculateNormals(); ringMesh.RecalculateBounds();
            var ringMaterial = new Material(shader) { name = "HOBA blue-white exhaust", color = _colors["exhaustBody"] };
            ringMaterial.EnableKeyword("_EMISSION");
            _materials.Add(ringMaterial);
            foreach (var jet in BoardDesigns.Jets(design))
            for (var i = 0; i < 3; i++)
            {
                var ring = new GameObject("Exhaust ring " + i);
                var node = _nodes[_family == "hoba" ? "static" : jet.Z > 0 ? "front" : "rear"];
                ring.transform.SetParent(node.Transform, false);
                ring.transform.localPosition = new Vector3(jet.X, jet.Y - .005f, jet.Z) - node.Pivot;
                ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                var renderer = ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ringMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _rings.Add((ring.transform, renderer, i));
            }
            var lightRoot = new GameObject("Thruster spill");
            lightRoot.transform.SetParent(Root.transform, false);
            lightRoot.transform.localPosition = new Vector3(0, BoardGeometry.ThrusterBottom + .01f, 0);
            _light = lightRoot.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _colors["spillLight"];
            _light.range = 1.15f;
            _light.shadows = LightShadows.None;
            _light.intensity = .6f;
            _scan = new GameObject("HOBA scan marker");
            _scan.SetActive(false);
            _scan.transform.SetParent(Root.transform, false);
            _scan.transform.localPosition = Vector3.up * .12f;
            var scannable = _scan.AddComponent<Nivalis.Scanning.Scannable>();
            scannable.notFromCanvas = true;
            scannable.customSprite = BoardItem.Types.GetValueOrDefault(BoardProducts.Appearance(designId, finishId).Id)?.icon;
            scannable.customColor = new Color(.15f,.85f,1);
            scannable.scaleMultiplier = .8f;
            if (scannable.customSprite != null) _compass = new BoardCompass(Root.transform, scannable.customSprite);
            _scan.SetActive(true);
            Pulse(1, 0);
        }
        catch { Dispose(); throw; }
    }

    internal void SetScanVisible(bool visible)
    {
        if (_scan != null && _scan.activeSelf != visible) _scan.SetActive(visible);
        _compass?.SetParked(visible);
    }

    private float _lastPulseTime = float.NaN;

    internal void Pulse(float pulse, float speedRatio, float bank = 0)
    {
        var now = Time.time;
        if (Time.timeScale <= 0 && now == _lastPulseTime) return;
        _lastPulseTime = now;
        TrackMotion();
        var turnRate = _motion.Count > 0 ? _motion[^1].TurnRate : 0;
        _steeringTurn = Mathf.Lerp(_steeringTurn, turnRate, 1 - Mathf.Exp(-Time.deltaTime / .07f));
        foreach (var (key, node) in _nodes)
        {
            if (key == "static") continue;
            var pose = BoardArticulation.Sample(Time.time, node.Pivot.z, speedRatio, bank);
            var delay = Mathf.InverseLerp(.405f, -.405f, node.Pivot.z) * (_family == "sc" ? .10f : .13f);
            var lag = DelayedMotion(delay);
            var displaced = Root.transform.InverseTransformPoint(lag.Position + lag.Rotation * node.Pivot) - node.Pivot;
            var blend = 1 - Mathf.Exp(-Time.deltaTime / (.025f + delay * .35f));
            var arc = BoardArticulation.SteeringArc(node.Pivot.z, _steeringTurn, key is "front" or "rear");
            // Keep acceleration stretch along the board, but do not offset the rear sideways:
            // that would fight the steering arc and produce the old S-shaped connection.
            var targetPosition = new Vector3(arc.X, node.Pivot.y + pose.Height,
                arc.Z + Mathf.Clamp(displaced.z, -.045f, .045f));
            // Each FC ring twists around its own longitudinal axis, with the same rearward delay.
            var twist = _family == "fc" && key.StartsWith("spine-") ? BoardArticulation.SpineTwist(lag.TurnRate) : 0;
            var targetRotation = Quaternion.Euler(0, arc.Yaw, pose.Roll + twist);
            node.Transform.localPosition = Vector3.Lerp(node.Transform.localPosition, targetPosition, blend);
            node.Transform.localRotation = Quaternion.Slerp(node.Transform.localRotation, targetRotation, blend);
        }
        foreach (var link in _links)
        {
            Vector3 Anchor(string key)
            {
                var node = _nodes[key];
                var rest = key == "front" ? new Vector3(link.X,.015f,.16f) : key == "rear" ? new Vector3(link.X,.015f,-.16f) : node.Pivot;
                return Root.transform.InverseTransformPoint(node.Transform.TransformPoint(rest - node.Pivot));
            }
            var a = Anchor(link.From); var b = Anchor(link.To);
            var length = Vector3.Distance(a, b);
            var fromDirection = _nodes[link.From].Transform.localRotation * Vector3.forward;
            var toDirection = _nodes[link.To].Transform.localRotation * Vector3.forward;
            var c1 = a + fromDirection * length / 3f;
            var c2 = b - toDirection * length / 3f;
            foreach (var surface in link.Surfaces)
                surface.Bend(a, c1, c2, b, _family == "sc" ? .028f : .012f, Time.time);
        }
        foreach (var (renderer, color) in _glowing)
        {
            _pulse.Clear();
            _pulse.SetColor("_Color", color * (.8f + .2f * pulse));
            _pulse.SetColor("_EmissionColor", color * (3.5f + speedRatio * 2) * pulse);
            renderer.SetPropertyBlock(_pulse);
        }
        foreach (var (transform, renderer, level) in _rings)
        {
            var wave = Mathf.Sin(Time.time * 6 + level * 1.4f);
            var envelope = .8f + .2f * wave;
            var p = transform.localPosition;
            p.y = BoardGeometry.ThrusterBottom - .025f - level * .065f + .003f * wave;
            transform.localPosition = p;
            // Three persistent, widely separated tiers taper downward.
            var scale = (1.2f - level * .3f) * (.96f + .04f * wave);
            transform.localScale = new Vector3(scale, 1, scale);
            _pulse.Clear();
            _pulse.SetColor("_EmissionColor", _colors["exhaust"] * envelope * (4 + speedRatio * 3));
            renderer.SetPropertyBlock(_pulse);
        }
        _light.intensity = (.5f + speedRatio * .3f) * pulse;
    }

    private void CreateLinks(Shader shader, bool legendary)
    {
        // Subdivide along the connector so the core can bend rather than just rotate.
        var core = new BoardPart("Curved field core", "core");
        var corners = new[] { new System.Numerics.Vector2(-.5f,-.5f), new System.Numerics.Vector2(.5f,-.5f),
            new System.Numerics.Vector2(.5f,.5f), new System.Numerics.Vector2(-.5f,.5f) };
        for (var section = 0; section < 20; section++)
        for (var side = 0; side < 4; side++)
        {
            var a = corners[side]; var b = corners[(side+1)%4];
            var z = section / 20f - .5f; var end = (section+1) / 20f - .5f;
            core.Quad(new(a.X,a.Y,z), new(b.X,b.Y,z), new(b.X,b.Y,end), new(a.X,a.Y,end));
        }
        var color = _colors["field"];
        var material = new Material(shader) { name = "HOBA energy tether", color = color };
        material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",color*4);
        _materials.Add(material);
        var chain = new[] { "rear" }.Concat(_nodes.Keys.Where(k => k.StartsWith("spine-")).OrderBy(k => _nodes[k].Pivot.z)).Append("front").ToArray();
        var offsets = _family == "sc" && legendary ? new[] { -.038f,.038f } : new[] { 0f };
        foreach (var x in offsets)
        for (var i=1;i<chain.Length;i++)
        {
            var obj = new GameObject("Field link " + i);
            obj.transform.SetParent(Root.transform,false);
            var surface = new CurvedFieldSurface(core, 0);
            _meshes.Add(surface.Mesh);
            obj.AddComponent<MeshFilter>().sharedMesh = surface.Mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _glowing.Add((renderer,color));
            var surfaces = new List<CurvedFieldSurface> { surface };
            _links.Add((obj.transform,x,chain[i-1],chain[i],surfaces));
            if (_family == "sc") CreateFieldShells(obj.transform, shader, surfaces);
        }
    }

    private void TrackMotion()
    {
        var now = Time.time;
        var position = Root.transform.position;
        if (_motion.Count > 0 && (_motion[^1].Time > now || now - _motion[^1].Time > .25f ||
            Vector3.Distance(position, _motion[^1].Position) > 3)) _motion.Clear();
        if (_motion.Count > 0 && now <= _motion[^1].Time) return;
        var velocity = _motion.Count == 0 ? Vector3.zero : (position - _motion[^1].Position) / (now - _motion[^1].Time);
        var turnRate = _motion.Count == 0 ? 0 : Mathf.DeltaAngle(_motion[^1].Rotation.eulerAngles.y,
            Root.transform.eulerAngles.y) / (now - _motion[^1].Time);
        _motion.Add((now, position, Root.transform.rotation, velocity, turnRate));
        while (_motion.Count > 2 && _motion[1].Time < now - .2f) _motion.RemoveAt(0);
    }

    private (Vector3 Position, Quaternion Rotation, float TurnRate) DelayedMotion(float delay)
    {
        if (_motion.Count < 2 || delay <= 0) return (Root.transform.position, Root.transform.rotation, 0);
        var target = Time.time - delay;
        var a = _motion[0]; var b = a;
        for (var i = 1; i < _motion.Count; i++)
        {
            b = _motion[i];
            if (b.Time >= target) break;
            a = b;
        }
        var t = Mathf.InverseLerp(a.Time, b.Time, target);
        var sampledTime = Mathf.Lerp(a.Time, b.Time, t);
        // Predict constant travel so the gap catches up; changes in speed/direction still ripple rearward.
        var position = Vector3.Lerp(a.Position, b.Position, t) + Vector3.Lerp(a.Velocity, b.Velocity, t) * (Time.time - sampledTime);
        return (position, Quaternion.Slerp(a.Rotation, b.Rotation, t), Mathf.Lerp(a.TurnRate, b.TurnRate, t));
    }

    private void CreateFieldShells(Transform link, Shader shader, List<CurvedFieldSurface> surfaces)
    {
        var material = new Material(shader) { name = "HOBA dim field sleeves", color = _colors["fieldSleeve"] };
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", _colors["fieldSleeveEmission"]);
        _materials.Add(material);
        for (var layer = 0; layer < 2; layer++)
        {
            var part = new BoardPart("Patterned open energy cylinder", "cyan");
            var radius = 1.05f + layer * .55f;
            for (var band = 0; band < 7; band++)
            for (var segment = 0; segment < 16; segment++)
            {
                if ((segment + band * 2 + layer) % 5 == 0) continue;
                var angle = segment * MathF.PI / 8;
                var end = angle + MathF.PI / 8 * .78f;
                var z = -.46f + band * .135f;
                System.Numerics.Vector3 V(float theta, float depth) => new(MathF.Cos(theta) * radius, MathF.Sin(theta) * radius, depth);
                var a = V(angle, z); var b = V(end, z); var c = V(end, z + .055f); var d = V(angle, z + .055f);
                part.Quad(a, b, c, d); part.Quad(d, c, b, a);
            }
            var surface = new CurvedFieldSurface(part, layer == 0 ? 65 : -42);
            surfaces.Add(surface);
            var mesh = surface.Mesh;
            _meshes.Add(mesh);
            var shell = new GameObject("Counter-rotating field sleeve " + layer);
            shell.transform.SetParent(link, false);
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
    }

    public void Dispose()
    {
        _compass?.Dispose(); _compass = null;
        if (Root != null) { Root.SetActive(false); Object.Destroy(Root); }
        foreach (var mesh in _meshes) if (mesh != null) Object.Destroy(mesh);
        foreach (var material in _materials) if (material != null) Object.Destroy(material);
        _meshes.Clear(); _materials.Clear(); _glowing.Clear(); _rings.Clear();
    }
}
