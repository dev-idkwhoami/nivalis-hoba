using System.Globalization;
using System.Numerics;
using NivalisMods.Hoba;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
FieldDeformationChecks.Run();
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Near(float actual, float expected, float tolerance, string message) => Check(Math.Abs(actual - expected) <= tolerance, message + $": {actual} vs {expected}");
RideMotion Run(float forward, float side, float seconds, int fps)
{
    var motion = new RideMotion();
    for (var i = 0; i < seconds * fps; i++) motion.Step(forward, side, 1f / fps);
    return motion;
}
Near(Run(1, 0, 1, 60).Forward, 4.5f, .001f, "Acceleration takes time");
Near(Run(1, 0, 4, 60).Forward, RideMotion.MaxSpeed, .001f, "Top speed cap");
Near(Run(1, 0, 1, 30).Forward, Run(1, 0, 1, 144).Forward, .001f, "Frame-rate independent acceleration");
Near(Run(0, 1, 2, 60).Forward, 0, 0, "Turning alone never translates the board");
Check(Run(1, 1, 5, 60).SpeedRatio <= 1, "No diagonal overspeed");
var braking = Run(1, 0, 3, 60);
for (var i = 0; i < 45; i++) braking.Step(-1, 0, 1f / 60);
Near(braking.Forward, 0, .001f, "S brakes forward motion through zero");
for (var i = 0; i < 60; i++) braking.Step(-1, 0, 1f / 60);
Near(braking.Forward, -4.5f, .001f, "Holding S accelerates backward after stopping");
Near(Run(-1, 0, 1, 60).Forward, -4.5f, .001f, "S accelerates backward from rest");
Near(Run(-1, 0, 4, 60).Forward, -RideMotion.MaxSpeed, .001f, "Reverse speed cap");
Check(Run(-1, 1, 5, 60).SpeedRatio <= 1, "No reverse diagonal overspeed");
var reverse = Run(-1, 0, 3, 60);
for (var i = 0; i < 105; i++) reverse.Step(1, 0, 1f / 60);
Near(reverse.Forward, 4.5f, .001f, "W brakes reverse then accelerates forward");
var changing30 = Run(1, 0, 3, 30); var changing144 = Run(1, 0, 3, 144);
for (var i = 0; i < 60; i++) changing30.Step(-1, 0, 1f / 30);
for (var i = 0; i < 288; i++) changing144.Step(-1, 0, 1f / 144);
Near(changing30.Forward, changing144.Forward, .002f, "Reversing is frame-rate independent");
var reverseCoast = Run(-1, 0, 3, 60);
for (var i = 0; i < 200; i++) reverseCoast.Step(0, 0, 1f / 60);
Near(reverseCoast.Forward, 0, .001f, "Reverse coasts to rest");
var coast = Run(1, 0, 3, 60);
coast.Step(0, 0, 1f / 60);
Check(coast.Forward > 8, "Releasing W coasts rather than instantly stopping");
for (var i = 0; i < 200; i++) coast.Step(0, 0, 1f / 60);
Near(coast.Forward, 0, .001f, "Coast comes to rest");
var blocked = Run(1, 1, 3, 60);
blocked.Blocked();
Near(blocked.Forward, 0, 0, "Collision clears stored speed");
blocked.Step(1, 0, 10);
Check(blocked.Forward <= .226f, "Stalled frame cannot launch the rider");
var frozen = blocked.Forward;
blocked.Step(1, 1, 0); blocked.Step(1, 1, -1); blocked.Step(1, 1, float.NaN);
Near(blocked.Forward, frozen, 0, "Invalid delta does not advance simulation");
blocked.Step(float.NaN, float.PositiveInfinity, .02f);
Check(float.IsFinite(blocked.Forward), "Invalid input cannot poison simulation");
var idle = new RideMotion(); var fast = Run(1, 0, 3, 60);
float idleMax = 0, fastMax = 0;
for (var i = 0; i < 600; i++)
{
    idle.Step(0, 0, 1f / 60); fast.Step(1, 0, 1f / 60);
    idleMax = Math.Max(idleMax, Math.Abs(idle.Hover)); fastMax = Math.Max(fastMax, Math.Abs(fast.Hover));
    Check(fast.Pulse >= .599f && fast.Pulse <= 1.001f, "Pulse bounded");
}
Check(idleMax > .01f && fastMax > idleMax * 3 && fastMax <= .046f, "Hover exists at idle and grows with speed");
Near(Run(1, 1, 3, 60).Bank, -8, .001f, "Right input banks right while moving");
Near(Run(1, -1, 3, 60).Bank, 8, .001f, "Left input banks left while moving");

// Crouch and steering tests exercise behavior independently of Unity input.
var tuck = new RideMotion();
for (var i = 0; i < 300; i++) tuck.Step(1, 1, 1f / 60, true);
Near(tuck.Crouch, 1, .001f, "Crouch blends fully");
Near(tuck.Forward, 10.35f, .001f, "Crouch gives fifteen percent speed bonus");
Near(tuck.TurnRate, 260, .001f, "Crouch increases steering authority");
Near(tuck.Bank, -14, .001f, "Crouched right turn leans further");
var steering = Run(1, 0, 3, 60);
for (var i = 0; i < 60; i++) steering.Step(1, -1, 1f / 60);
Near(steering.Bank, 8, .001f, "A steering banks left without strafe");
for (var i = 0; i < 120; i++) tuck.Step(1, 0, 1f / 60);
Near(tuck.Crouch, 0, .001f, "Releasing Shift restores upright stance");
Near(tuck.Forward, 9, .001f, "Releasing Shift restores normal cap");
Near(tuck.Bank, 0, .001f, "Straight riding levels the board");
var terrain = new GroundResponse();
terrain.Step(0, 0, 0, 0);
for (var i = 0; i < 240; i++) terrain.Step(i % 2 == 0 ? .01f : -.01f, .04f, -.04f, 1f / 60);
Near(terrain.Height, 0, 0, "Small ground chatter is ignored");
Near(terrain.ForwardSlope, 0, 0, "Small pitch chatter is ignored");
Near(terrain.CrossSlope, 0, 0, "Small roll chatter is ignored");
for (var i = 0; i < 120; i++) terrain.Step(.2f, .3f, -.2f, 1f / 60);
Near(terrain.Height, .2f, .013f, "Suspension follows sustained elevation");
Near(terrain.ForwardSlope, .3f, .001f, "Real uphill slope retained");
Near(terrain.CrossSlope, -.2f, .001f, "Real cross slope retained");
Near(GroundResponse.Median(0, .15f, 0), 0, 0, "Single raised paver rejected");
Near(GroundResponse.Median(-.1f, 0, .1f), 0, 0, "Linear slope center preserved");
terrain.Reset(); terrain.Step(3, -.4f, 0, 0);
Near(terrain.Height, 3, 0, "Remount initializes at new ground");
Near(terrain.ForwardSlope, -.4f, 0, "Downhill slope preserved");

Near(Run(1, 0, 3, 60).YawDelta, 0, 0, "No steering input cannot change board heading");
Near(Run(1, 1, 3, 60).YawDelta * 60, 160, .001f, "D turns right at configured yaw rate");
Near(Run(1, -1, 3, 60).YawDelta * 60, -160, .001f, "A turns left at configured yaw rate");
float CoastDistance(float speedSign, int fps, bool crouch = false)
{
    var m = new RideMotion();
    for (var i = 0; i < fps * 4; i++) m.Step(speedSign, 0, 1f / fps, crouch);
    var initial = m.Forward;
    var distance = 0f;
    for (var i = 0; i < fps * 6; i++) distance += m.Coast(1f / fps);
    Near(m.Forward, 0, 0, "Unattended board eventually stops");
    Near(distance, MathF.CopySign(initial * initial / 6, initial), .002f, "Coast preserves momentum with consistent friction");
    return distance;
}
Near(CoastDistance(1, 30), CoastDistance(1, 144), .002f, "Coasting is frame-rate independent");
Near(CoastDistance(-1, 60), -13.5f, .002f, "Reverse momentum survives dismount");
Check(CoastDistance(1, 60, true) > 17, "Crouch momentum survives dismount without speed clamp");
var afterCollision = Run(1, 0, 3, 60); afterCollision.Blocked();
Near(afterCollision.Coast(.02f), 0, 0, "Blocked parked board no longer travels");
Check(RideMotion.HoverHeight > .30f && RideMotion.UnloadedHeight > RideMotion.HoverHeight + .2f,
    "Mounted clearance increased and unloaded board floats substantially higher");
// Regression: the old 30 cm filter reset caused repeated drops on fast descents.
foreach (var sign in new[] { -1, 1 })
{
    var slope = new GroundResponse(); slope.Step(0, sign * .5f, 0, 0);
    var lastDelta = 0f;
    for (var i = 1; i <= 360; i++)
    {
        var old = slope.Height;
        slope.Step(sign * i * .075f, sign * .5f, 0, 1f / 60);
        var delta = slope.Height - old;
        Check(Math.Abs(delta) < .076f, "Fast slope never snaps to raw height");
        if (i > 120) Near(delta, lastDelta, .0001f, "Steady fast slope has no repeated camera steps");
        lastDelta = delta;
    }
}

// Look sector follows board yaw without wrapping through the forbidden side.
var look = new RideLook();
Near(look.Step(0, 0, 0, .016f), 0, 0, "Forward view remains forward");
Near(look.Step(-60, 0, 0, .016f), -45, .001f, "Left view stops at 45 degrees");
Near(look.Step(-46, 0, 0, .016f), -45, .001f, "Repeated left input cannot cross the limit");
look.Reset(); look.Step(170, 0, 0, .016f);
Near(look.Step(-175, 0, 0, .016f), 180, .001f, "Right/rear limit survives world yaw wrap");
Near(look.Step(-179, 0, 0, .016f), 180, .001f, "Rear boundary cannot jump to left boundary");
Near(look.Step(179, 0, 0, .016f), 179, .001f, "Can look back out of rear limit immediately");
look.Reset(); look.Step(90, 0, 0, .016f);
Near(look.Step(90, 20, 0, .016f), 90, .001f, "Ordinary turn preserves world view inside sector");
look.Reset(); look.Step(355, 350, 0, .016f);
Near(RideLook.Delta(355, look.Step(355, 10, 0, .016f)), 0, .001f, "Board wrap does not tug normal free look");
float CornerLook(int fps)
{
    var l = new RideLook(); var view = l.Step(120, 0, 0, 0);
    for (var i = 1; i <= fps; i++) view = l.Step(view, 260f * i / fps, 1, 1f / fps);
    Check(l.Relative > -35 && l.Relative < -15, "Sustained sharp turn has bounded smooth camera lag");
    return l.Relative;
}
Near(CornerLook(30), CornerLook(144), 4, "Corner lag remains comparable at 30 and 144 FPS");
look.Reset(); look.Step(120, 0, 0, 0);
var pulled = look.Step(120, 4, 1, 1f / 60);
Check(look.Relative > 100 && look.Relative < 120, "Initial corner pull is smooth, not a snap");
Near(look.Step(pulled, 8, 0, 1f / 60), pulled, .001f, "Normal steering immediately releases camera pull");
look.Reset(); look.Step(30, 0, 0, 0);
Near(look.Step(30, 0, 1, 0), 30, 0, "Zero time cannot advance camera centering");
Near(look.Step(float.NaN, 0, 1, .02f), 30, 0, "Invalid view does not poison camera state");

var idleLook = new RideLook();
var idleView = idleLook.Step(90, 0, 0, 0);
for (var i = 0; i < 54; i++) { idleLook.ObserveInput(false, 1f / 60); idleView = idleLook.Step(idleView, 0, 0, 1f / 60); }
Near(idleLook.AutoFollow, 0, 0, "Idle follow waits a full second");
Near(idleView, 90, .001f, "Free look stays put before idle delay");
for (var i = 0; i < 180; i++) { idleLook.ObserveInput(false, 1f / 60); idleView = idleLook.Step(idleView, i, 0, 1f / 60); }
Near(idleLook.AutoFollow, 1, .001f, "Idle follow blends to full strength");
Check(Math.Abs(idleLook.Relative) < 8, "One hand steering follows with a small deliberate lag");
idleLook.ObserveInput(true, 1f / 60);
Near(idleLook.AutoFollow, 0, 0, "Yaw or pitch input immediately cancels idle follow");
for (var i = 0; i < 54; i++) idleLook.ObserveInput(false, 1f / 60);
Near(idleLook.AutoFollow, 0, 0, "Mouse movement restarts the full delay");
idleLook.ObserveInput(false, 0);
Near(idleLook.AutoFollow, 0, 0, "Paused time cannot activate following");
idleLook.Reset();
Near(idleLook.AutoFollow, 0, 0, "Remount clears the idle follow blend");

// Keyboard jitter regression: alternating steering should be filtered, not replayed.
var pursuit = new RideLook();
for (var i = 0; i < 120; i++) pursuit.ObserveInput(false, 1f / 60);
var pursuitView = pursuit.Step(0, 0, 0, 0);
float boardYaw = 0, cameraTravel = 0, boardTravel = 0, previousCameraSpeed = 0, biggestSpeedChange = 0;
for (var i = 0; i < 240; i++)
{
    var increment = i % 12 < 6 ? 160f / 60 : -160f / 60;
    boardYaw += increment; boardTravel += Math.Abs(increment);
    var next = pursuit.Step(pursuitView, boardYaw, 0, 1f / 60);
    var speed = RideLook.Delta(pursuitView, next) * 60;
    cameraTravel += Math.Abs(RideLook.Delta(pursuitView, next));
    biggestSpeedChange = Math.Max(biggestSpeedChange, Math.Abs(speed - previousCameraSpeed));
    previousCameraSpeed = speed; pursuitView = next;
}
Check(cameraTravel < boardTravel * .5f, "Rapid AD reversals substantially reduce camera travel");
Check(biggestSpeedChange < 100, "Direction reversals do not instantly flip camera speed");
for (var i = 0; i < 90; i++) pursuitView = pursuit.Step(pursuitView, boardYaw, 0, 1f / 60);
Near(pursuitView, boardYaw, .001f, "Latest heading wins; old turns are not queued");
pursuit.ObserveInput(true, 1f / 60);
Near(pursuit.Step(pursuitView + 20, boardYaw, 0, 1f / 60), pursuitView + 20, .001f,
    "Manual look immediately clears follow momentum");

// Terrain pitch is additive and reversible, including native pitch-limit clipping.
foreach (var slopeSign in new[] { -1, 1 })
{
    var pitch = new RidePitch(); var viewPitch = 15f;
    var slope = slopeSign * MathF.Tan(20 * MathF.PI / 180);
    for (var i = 0; i < 180; i++) viewPitch = pitch.Step(viewPitch, slope, 1, 1f / 60, -70, 70);
    Near(viewPitch, 15 - slopeSign * 20, .001f, "Terrain adds the matching signed slope angle");
    Near(pitch.WithoutOffset(viewPitch), 15, .001f, "Dismount removes only terrain contribution");
    for (var i = 0; i < 180; i++) viewPitch = pitch.Step(viewPitch, 0, 1, 1f / 60, -70, 70);
    Near(viewPitch, 15, .001f, "Level ground restores original look angle");
}
var clippedPitch = new RidePitch(); var clippedView = -65f;
for (var i = 0; i < 180; i++) clippedView = clippedPitch.Step(clippedView, .5f, 1, 1f / 60, -70, 70);
Near(clippedView, -70, 0, "Slope respects native pitch limit");
for (var i = 0; i < 180; i++) clippedView = clippedPitch.Step(clippedView, 0, 1, 1f / 60, -70, 70);
Near(clippedView, -65, .001f, "Clipping does not corrupt remembered angle");
var manualPitch = new RidePitch(); var manualView = 10f;
for (var i = 0; i < 180; i++) manualView = manualPitch.Step(manualView, .4f, 1, 1f / 60, -70, 70);
manualView += 7;
for (var i = 0; i < 180; i++) manualView = manualPitch.Step(manualView, .4f, 0, 1f / 60, -70, 70);
Near(manualView, 17, .001f, "Manual input changes baseline and leaving one-hand mode removes offset");
Near(manualPitch.Step(manualView, -.5f, 1, 0, -70, 70), manualView, .001f, "Paused terrain pitch stays frozen");
float PitchAt(int fps)
{
    var pitch = new RidePitch(); var value = 12f;
    for (var i = 0; i < fps / 2; i++) value = pitch.Step(value, .4f, 1, 1f / fps, -70, 70);
    return value;
}
Near(PitchAt(30), PitchAt(144), .001f, "Slope-pitch smoothing is frame-rate independent");

// Sustained look input and terrain following cannot push riding pitch into either pole.
foreach (var direction in new[] { -1, 1 })
{
    var limitPitch = new RidePitch();
    var angle = 0f;
    for (var frame = 0; frame < 120; frame++)
    {
        angle = limitPitch.Step(angle + direction * 12, -direction * .6f, 1, 1f / 60,
            -RidePitch.LookLimit, RidePitch.LookLimit);
        Check(Math.Abs(angle) <= 80, "Look input and slopes respect polar exclusion");
    }
    Near(angle, direction * 80, .001f, "Both riding look limits are reachable");
    var away = limitPitch.Step(angle - direction * 30, 0, 0, 1f / 60, -RidePitch.LookLimit, RidePitch.LookLimit);
    Check(Math.Abs(away) < 80, "Look can leave the pole boundary immediately");
}

// Configured catalog values retain persistent identity and affect runtime consumers.
var originalProducts = BoardProducts.All.ToArray();
var tuned = new RideMotion(new RideProfile(Speed: 14, Acceleration: 8, TurnRate: 90, CrouchedTurnRate: 120, CrouchSpeedBonus: .2f));
for (var i = 0; i < 500; i++) tuned.Step(1, 1, .02f);
Near(tuned.Forward, 14, .001f, "Configured top speed");
Near(tuned.YawDelta, 1.8f, .001f, "Configured standing turn rate");
for (var i = 0; i < 500; i++) tuned.Step(1, 1, .02f, true);
Near(tuned.Forward, 16.8f, .001f, "Configured crouch speed bonus");
Near(tuned.YawDelta, 2.4f, .001f, "Configured crouched turn rate");
var configuredCoast = new RideMotion(new RideProfile(Acceleration: 10, CoastDeceleration: 2));
configuredCoast.Step(1, 0, .05f);
Near(configuredCoast.Forward, .5f, .001f, "Configured acceleration");
configuredCoast.Coast(.05f);
Near(configuredCoast.Forward, .4f, .001f, "Configured parked deceleration");
BoardTuning.Prices[BoardDesigns.DefaultId] = 4321;
BoardTuning.Prices["paint.industrial"] = 456;
BoardProducts.ApplyConfiguredPrices();
Check(BoardProducts.Appearance(BoardDesigns.DefaultId, "industrial").Price == 4321, "Painted board inherits model price");
Check(BoardProducts.All.Single(p => p.IsPaint && p.Finish == "industrial").Price == 456, "Paint price configured separately");
Check(BoardProducts.All.Select(p => (p.Id, p.ForSale)).SequenceEqual(originalProducts.Select(p => (p.Id, p.ForSale))), "Price configuration preserves IDs and sale restrictions");
Array.Copy(originalProducts, BoardProducts.All, originalProducts.Length); BoardTuning.Prices.Clear();
var customColor = new Vector3(.125f, .5f, .75f);
Check(BoardTuning.TryColor(BoardTuning.FormatColor(customColor), out var parsedColor) && parsedColor == customColor, "RGB config roundtrip");
foreach (var invalid in new[] { "NaN,0,0", "1,2,0", "1,0", "red", "Infinity,0,0" })
    Check(!BoardTuning.TryColor(invalid, out _), "Invalid RGB rejected");
BoardTuning.Palettes["industrial"] = new() { ["shell"] = customColor };
Check(BoardDesigns.Palette(BoardDesigns.Get(BoardDesigns.DefaultId), "industrial")["shell"] == customColor, "Configured material color applied");
Check(BoardDesigns.Palette(BoardDesigns.Get(BoardDesigns.DefaultId))["shell"] != customColor, "Other palettes unchanged");
BoardTuning.Palettes.Clear();

// Production placements are independent of editor exports.
Check(WorldPlacements.Chests.Length == 3 && WorldPlacements.Chests.Select(p => p.Id).Distinct().Count() == 3, "Three unique embedded chests");
Check(WorldPlacements.Chests.Select(p => p.Area).Distinct().Count() == 3, "Legendary chests occupy three distinct areas");
Check(WorldPlacements.Chests.Select(p => p.InteractionRange).SequenceEqual(new[] { 1.5f, 1.5f, 1f }), "Authored interaction ranges are embedded exactly");
var checkpoint = new HobaCheckpoint { Board = new ParkedBoardSave(BoardProducts.LegacyId, "7_Sewers", new[] { 14f, 18f, -42f }, new[] { 0f, 0f, 0f, 1f }) };
checkpoint.Discoveries[WorldPlacements.Chests[0].Id] = BoardProducts.All.First(p => p.Legendary).Id;
var encodedCheckpoint = HobaCheckpointBinary.Encode(checkpoint);
var restoredCheckpoint = HobaCheckpointBinary.Decode(encodedCheckpoint);
Check(restoredCheckpoint.Board!.Ground.SequenceEqual(checkpoint.Board.Ground) && restoredCheckpoint.Board.Area == "7_Sewers", "Parked board position and area survive restart data serialization");
Check(restoredCheckpoint.Discoveries.SequenceEqual(checkpoint.Discoveries), "Legendary unlocks survive checkpoint serialization");

Check(HobaCheckpointBinary.Decode(HobaCheckpointBinary.Encode(new HobaCheckpoint())).Board == null, "Empty binary checkpoint round trip");
foreach (var invalid in new[] { encodedCheckpoint[..^1], encodedCheckpoint.Concat(new byte[] { 0 }).ToArray(), new byte[16] })
{
    var rejected = false;
    try { HobaCheckpointBinary.Decode(invalid); }
    catch (Exception e) when (e is IOException or InvalidDataException) { rejected = true; }
    Check(rejected, "Malformed binary checkpoint rejected");
}
var legacyCheckpoint = System.Text.Json.JsonSerializer.Deserialize<HobaCheckpoint>(System.Text.Json.JsonSerializer.Serialize(checkpoint))!;
Check(HobaCheckpointBinary.Decode(HobaCheckpointBinary.Encode(legacyCheckpoint)).Board!.Product == checkpoint.Board.Product, "Legacy JSON progress converts to binary");
var discoveries = new HobaCheckpoint();
Check(!discoveries.TryDiscover(WorldPlacements.Chests[0].Id, _ => false, out _) && discoveries.Discoveries.Count == 0, "Full inventory does not consume a legendary discovery");
foreach (var chest in WorldPlacements.Chests)
{
    Check(discoveries.TryDiscover(chest.Id, _ => true, out var reward) && reward!.Legendary, "Chest grants a legendary board");
    Check(!discoveries.TryDiscover(chest.Id, _ => throw new Exception("Duplicate reward"), out _), "Searched chest cannot grant twice");
}
Check(discoveries.Discoveries.Values.Distinct().Count() == 3, "Three chests unlock all three different legendaries");
discoveries.Validate(); restoredCheckpoint.Validate();

var parts = BoardGeometry.Create();
Check(parts.Count(p => p.Name.StartsWith("White core")) == 4, "Four propulsor cores");
Check(parts.Count(p => p.Name.StartsWith("Thruster collar")) == 4, "Four propulsor housings");
var thrusterParts = parts.Where(p => p.Name.StartsWith("Thruster") || p.Name.StartsWith("Blue nozzle") || p.Name.StartsWith("White core"));
Check(thrusterParts.SelectMany(p => p.Vertices).All(v => Math.Abs(v.X) <= .255f), "Jet housings fit inside the deck width");
Check(parts.Where(p => p.Name.StartsWith("Cable ")).SelectMany(p => p.Vertices).All(v => Math.Abs(v.X) < .255f), "Cable runs remain inside the board silhouette");
foreach (var part in parts)
{
    Check(BoardGeometry.Colors.ContainsKey(part.Finish), "Known material: " + part.Name);
    Check(part.Triangles.Count % 3 == 0 && part.Vertices.Count > 0, "Valid mesh: " + part.Name);
    for (var i = 0; i < part.Triangles.Count; i += 3)
    {
        var a = part.Vertices[part.Triangles[i]];
        var b = part.Vertices[part.Triangles[i + 1]];
        var c = part.Vertices[part.Triangles[i + 2]];
        Check(float.IsFinite(a.X + a.Y + a.Z), "Finite vertex");
        var normal = Vector3.Cross(b - a, c - a);
        Check(normal.LengthSquared() > 1e-16f, "Nondegenerate triangle: " + part.Name);
        Check(new[] { normal.X, normal.Y, normal.Z }.Count(v => Math.Abs(v) > 1e-9f) == 1,
            "Block face is axis-aligned, never beveled: " + part.Name);
        Check(new[] { a.X, a.Y, a.Z }.All(v => Math.Abs(v / BoardGeometry.Grid - MathF.Round(v / BoardGeometry.Grid)) < .0001f),
            "Vertex lies on the block grid");
    }
}
// Material parts now form one solid together. Rasterize exported quads back to
// grid faces to catch overlapping planes (z-fighting), holes and reversed winding.
var surfaceFaces = new Dictionary<(int Axis, int Plane, int U, int V), int>();
foreach (var part in parts)
for (var i = 0; i < part.Vertices.Count; i += 6)
{
    var q = part.Vertices.Skip(i).Take(6).ToArray();
    var n = Vector3.Cross(q[1] - q[0], q[2] - q[0]);
    var axis = Math.Abs(n.X) > 1e-9f ? 0 : Math.Abs(n.Y) > 1e-9f ? 1 : 2;
    float Coord(Vector3 p, int a) => a == 0 ? p.X : a == 1 ? p.Y : p.Z;
    int Cell(float v) => (int)MathF.Round(v / BoardGeometry.Grid);
    var uAxis = axis == 0 ? 1 : 0; var vAxis = axis == 2 ? 1 : 2;
    var plane = Cell(Coord(q[0], axis));
    for (var u = Cell(q.Min(p => Coord(p, uAxis))); u < Cell(q.Max(p => Coord(p, uAxis))); u++)
    for (var v = Cell(q.Min(p => Coord(p, vAxis))); v < Cell(q.Max(p => Coord(p, vAxis))); v++)
        Check(surfaceFaces.TryAdd((axis, plane, u, v), Math.Sign(Coord(n, axis))), "No overlapping grid faces across materials");
}
foreach (var ray in surfaceFaces.GroupBy(f => (f.Key.Axis, f.Key.U, f.Key.V)))
{
    var crossings = ray.OrderBy(f => f.Key.Plane).Select(f => f.Value).ToArray();
    Check(crossings.Length % 2 == 0, "Combined board surface is closed along every grid ray");
    for (var i = 0; i < crossings.Length; i++)
        Check(crossings[i] == (i % 2 == 0 ? -1 : 1), "Combined surface faces point out of the solid");
}
var jetAssembly = parts.Where(p => p.Name.StartsWith("Thruster") || p.Name.StartsWith("Nozzle well") || p.Name.StartsWith("Blue nozzle") || p.Name.StartsWith("White core")).SelectMany(p => p.Vertices).ToArray();
var jetHeight = jetAssembly.Max(v => v.Y) - jetAssembly.Min(v => v.Y);
Check(jetHeight >= .075f && jetHeight <= .095f, "Whole jet assembly is about half its previous 17 cm height");
Near(jetAssembly.Min(v => v.Y), BoardGeometry.ThrusterBottom, .001f, "Exhaust ring anchor tracks new nozzle bottom");
Near(BoardGeometry.KickHeight(.72f), .12f, .001f, "Nose rises twelve centimetres");
Near(BoardGeometry.KickHeight(-.72f), .12f, .001f, "Tail has matching rise");
Near(BoardGeometry.KickHeight(.45f), 0, 0, "Standing area remains flat");
Check(BoardGeometry.KickHeight(.66f) - BoardGeometry.KickHeight(.60f) > BoardGeometry.KickHeight(.60f) - BoardGeometry.KickHeight(.54f), "End profile curves progressively upward");
var deckShell = parts.First(p => p.Name == "Deck shell");
Check(deckShell.Vertices.Where(v => Math.Abs(v.Z) > .68f).Max(v => v.Y) > .14f, "Exported deck includes the raised ends");

var all = parts.SelectMany(p => p.Vertices).ToArray();
Near(all.Max(v => v.Z) - all.Min(v => v.Z), 1.44f, .001f, "Board length");
Check(all.Min(v => v.Y) + RideMotion.HoverHeight - .045f > .04f, "Thrusters clear flat ground at lowest hover");
var grip = parts.First(p => p.Name.StartsWith("Grip pad"));
Check(grip.Vertices.Max(v => v.Y) > .045f, "Grip pads above central deck");
var parked = new ParkingSpot("street", new Vector3(4, 0, 8), Quaternion.Identity);
Check(parked.IsVisible("street", false, true), "Parked board visible in its area");
Check(!parked.IsVisible("interior", false, true), "Board stays outside when entering another area");
Check(!parked.IsVisible("street", true, true), "Hide parked board during loading");
Check(!parked.IsVisible("street", false, false), "Hide parked board at title screen");
Check(parked.IsVisible("street", false, true), "Returning to reloaded area restores visibility");
Check(parked.Ground == new Vector3(4, 0, 8), "Area round trip preserves parked position");
Check(parked.IsNear(new Vector3(5, 0, 8)), "Mount nearby board");
Check(!parked.IsNear(new Vector3(7, 0, 8)), "Cannot recall distant board");
Check(!parked.IsNear(new Vector3(4, 3, 8)), "Cannot mount through a floor");
Check(!parked.IsNear(new Vector3(float.NaN, 0, 8)), "Invalid coordinates cannot mount");

// Hover audio envelope: bounded boost and smooth release, independent of frame rate.
var hoverVolume = new HoverVolume();
for (var i = 0; i < 120; i++) hoverVolume.Step(false, .05f, .02f, 1f / 60);
Near(hoverVolume.Value, .05f, .00001f, "Idle audio settles at configured base volume");
hoverVolume.Step(true, .05f, .02f, 1f / 60);
Check(hoverVolume.Value > .05f && hoverVolume.Value < .07f, "Acceleration volume rises smoothly");
for (var i = 0; i < 600; i++) hoverVolume.Step(true, .05f, .02f, 1f / 60);
Near(hoverVolume.Value, .07f, .00001f, "Acceleration boost does not stack");
hoverVolume.Step(false, .05f, .02f, 1f / 60);
Check(hoverVolume.Value > .05f && hoverVolume.Value < .07f, "Volume returns smoothly rather than snapping");
for (var i = 0; i < 600; i++) hoverVolume.Step(false, .05f, .02f, 1f / 60);
Near(hoverVolume.Value, .05f, .00001f, "Cruise returns to base volume");
var v30 = new HoverVolume(); var v120 = new HoverVolume();
for (var i = 0; i < 30; i++) v30.Step(true, .1f, .04f, 1f / 30);
for (var i = 0; i < 120; i++) v120.Step(true, .1f, .04f, 1f / 120);
Near(v30.Value, v120.Value, .00001f, "Volume envelope is frame-rate independent");
var soundMotion = new RideMotion();
soundMotion.Step(-1, 0, .05f);
Check(soundMotion.Accelerating, "Reverse acceleration also boosts the loop");
soundMotion.Step(1, 0, .01f);
Check(!soundMotion.Accelerating, "Braking does not boost the loop");
soundMotion.Coast(.05f);
Check(!soundMotion.Accelerating, "Coasting does not boost the loop");

Console.WriteLine($"HOBA: behavior/configuration checks and mesh validation passed ({parts.Length} parts, {all.Length} vertices, {parts.Sum(p => p.Triangles.Count / 3)} triangles). Native shop/pickup/save behavior requires in-game testing.");

if (args.Length == 2 && args[0] == "--export")
{
    Directory.CreateDirectory(args[1]);
    using var obj = new StreamWriter(Path.Combine(args[1], "hoba-01.obj"));
    obj.WriteLine("# HOBA-01, metres, +Y up, +Z forward. Generated from BoardGeometry.cs.");
    obj.WriteLine("mtllib hoba-01.mtl");
    var start = 1;
    foreach (var part in parts)
    {
        obj.WriteLine("o " + part.Name.Replace(' ', '_'));
        obj.WriteLine("usemtl " + part.Finish);
        foreach (var v in part.Vertices) obj.WriteLine($"v {v.X:R} {v.Y:R} {v.Z:R}");
        for (var i = 0; i < part.Triangles.Count; i += 3)
            obj.WriteLine($"f {start + part.Triangles[i]} {start + part.Triangles[i + 1]} {start + part.Triangles[i + 2]}");
        start += part.Vertices.Count;
    }
    using var mtl = new StreamWriter(Path.Combine(args[1], "hoba-01.mtl"));
    foreach (var (name, c) in BoardGeometry.Colors)
    {
        mtl.WriteLine($"newmtl {name}\nKd {c.X:R} {c.Y:R} {c.Z:R}\nKs 0.2 0.2 0.2\nNs 40");
        if (name is "cyan" or "core") mtl.WriteLine($"Ke {c.X * 4:R} {c.Y * 4:R} {c.Z * 4:R}");
    }
    Console.WriteLine("Exported HOBA-01 OBJ/MTL to " + Path.GetFullPath(args[1]));
}

ModelChecks.Run(args.Length == 2 && args[0] == "--export-catalog" ? args[1] : null);

ProductChecks.Run();

NativeChestChecks.Run();

var fov = new SpeedFov();
Near(fov.Step(0, 10, .02f), 0, .0001f, "Stationary board adds no FOV");
var firstFov = fov.Step(1, 10, .02f);
Check(firstFov > 0 && firstFov < 1, "Speed FOV blends without snapping");
for (var i = 0; i < 300; i++) fov.Step(1, 10, .02f);
Near(fov.Bonus, 10, .001f, "FOV slider controls additive maximum");
Near(fov.Step(0, 0, 0), 10, .001f, "Paused FOV holds its blend");
for (var i = 0; i < 300; i++) fov.Step(1, 0, .02f);
Near(fov.Bonus, 0, .001f, "Zero slider smoothly disables FOV bonus");
fov.Reset();
Near(fov.Bonus, 0, .0001f, "Dismount resets FOV bonus");
