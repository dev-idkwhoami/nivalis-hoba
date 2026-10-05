using Cinemachine;
using Il2CppInterop.Runtime.Attributes;
using Nivalis;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NivalisMods.Hoba;

public sealed class RideController : MonoBehaviour
{
    internal static RideController? Instance;
    private PlayerCharacter? _player;
    private PlayerCharacterController? _controller;
    private BoardModel? _board;
    private RideMotion? _motion;
    private CinemachineTransposer? _transposer;
    private CinemachineVirtualCamera? _camera;
    private OverrideableBool.OverrideLock? _handsLock, _interactionLock;
    private Vector3 _followOffset, _lastPosition, _normal = Vector3.up;
    private float _dutch, _yaw, _visualBlend;
    private bool _headBob;
    private int _scene;
    private ParkingSpot? _parking;
    private RideMotion? _idle;
    private string _boardArea = "";
    private Vector3 _parkGround;
    private Quaternion _parkRotation;
    private bool _hasPose;
    private float _parkHeight = RideMotion.UnloadedHeight;
    private int _groundMask;
    private float _surfaceHeight;
    private float _rideHeight = RideMotion.HoverHeight;
    private readonly GroundResponse _ground = new();
    private readonly RideLook _look = new();
    private readonly RidePitch _pitch = new();
    private float _turnStrength, _cameraBank;
    private bool _wasSuspended, _lookPoseReady;
    private int _resumeFrame = -1;
    private Vector2 _lookPose;
    private int _lookInputFrame = -1;
    private bool _horizontalRecentering, _verticalRecentering;

    public RideController(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;

    public void Update()
    {
        try
        {
            if (_controller != null && !ValidRide()) Dismount("activity or scene changed");
            if (_motion != null && (_controller == null || _board?.Root == null)) Dismount("player unloaded");
            if (_parking != null && _board?.Root == null) ResetBoard("board object was destroyed");
            UpdateParked();
            _board?.Audio.Tick(_motion?.Accelerating == true, _motion != null);
            if (GameplayAvailable()) BoardItem.Tick(this);
            SprayTool.Tick(this);
            if (!GameplayAvailable())
            {
                if (_motion != null) _wasSuspended = true;
                return;
            }
            // A click on Resume must never double as a board dismount.
            if (_wasSuspended)
            {
                _wasSuspended = false;
                _resumeFrame = Time.frameCount;
                return;
            }
            if (_resumeFrame == Time.frameCount || SprayTool.ConsumesInput || HiddenChests.HasSearchTarget) return;
            if (Mouse.current?.leftButton.wasPressedThisFrame != true) return;
            if ((Keyboard.current?.leftShiftKey.isPressed == true || Keyboard.current?.rightShiftKey.isPressed == true) &&
                (_motion != null || CanReachParked()))
            { if (BoardItem.Retrieve()) ResetBoard("put away"); }
            else if (SprayTool.Equipped) SprayTool.Click(this);
            else if (_motion != null) Dismount("left click");
            else if (CanReachParked()) Mount();
        }
        catch (Exception e) { Fail(e); }
    }

    public void LateUpdate()
    {
        try { SprayTool.Present(); }
        catch (Exception e) { Plugin.Logger.LogError("HOBA spray pose: " + e); SprayTool.Unequip(); }
    }

    private RideProfile Profile => BoardTuning.Profile(BoardItem.Deployed?.Model);

    internal bool HasBoard => _board != null;
    internal float FovSpeedRatio => _motion?.SpeedRatio ?? 0;
    internal bool IsMounted => _motion != null;
    internal Vector3 PaintPoint => _board!.Root.transform.position + _board.Root.transform.up * .08f;

    [HideFromIl2Cpp]
    internal ParkedBoardSave? CaptureParking()
    {
        if (_board == null || BoardItem.Deployed == null || string.IsNullOrEmpty(_boardArea)) return null;
        var p = _parkGround; var q = _parkRotation;
        return new ParkedBoardSave(BoardItem.Deployed.Id, _boardArea, new[] { p.x, p.y, p.z }, new[] { q.x, q.y, q.z, q.w });
    }

    [HideFromIl2Cpp]
    internal void RestoreParking(ParkedBoardSave saved)
    {
        var p = saved.Ground; var q = saved.Rotation;
        _parkGround = new Vector3(p[0],p[1],p[2]);
        _parkRotation = new Quaternion(q[0],q[1],q[2],q[3]);
        _boardArea = saved.Area;
        _parking = new ParkingSpot(saved.Area, new(p[0],p[1],p[2]), new(q[0],q[1],q[2],q[3]));
        _idle = new RideMotion(Profile); _parkHeight = Profile.UnloadedHeight;
        _groundMask = PlayerManager._instance.LocalPlayer.Character.Controller.groundCollisionMask;
        _yaw = _parkRotation.eulerAngles.y; _hasPose = true;
        _board = BoardItem.CreateModel();
        UpdateParked();
        Plugin.Logger.LogInfo("Restored parked HOBA in " + saved.Area);
    }

    [HideFromIl2Cpp]
    internal bool RepaintParked(BoardProduct paint)
    {
        var board = BoardItem.Deployed;
        if (!CanReachParked() || board == null || !BoardProducts.CanPaint(board, paint) || !BoardItem.HasPaint(paint)) return false;
        // Prepare all Unity resources before touching the saved item or can.
        var replacement = new BoardModel(board.Model!, paint.Finish);
        replacement.Root.SetActive(false);
        try
        {
            replacement.Root.transform.SetPositionAndRotation(_board!.Root.transform.position, _board.Root.transform.rotation);
            if (!BoardItem.ApplyDeployedPaint(paint)) { replacement.Dispose(); return false; }
        }
        catch { replacement.Dispose(); throw; }
        var previous = _board;
        _board = replacement;
        replacement.Root.SetActive(true);
        previous?.Dispose();
        return true;
    }
    [HideFromIl2Cpp]
    internal void UseBoard()
    {
        if (HasBoard) { BoardItem.Notify("Your board is already deployed. Shift-click it to put it away."); return; }
        DeployFromItem();
    }

    [HideFromIl2Cpp]
    private void DeployFromItem()
    {
        var player = PlayerManager._instance.LocalPlayer.Character;
        if (!CanRide(player)) return;
        var controller = player.Controller;
        var area = CurrentArea();
        if (string.IsNullOrEmpty(area)) return;
        // Spawn at the player's known walkable location; never across a wall or ledge.
        var p = controller.transform.position;
        if (!Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, 2.5f,
                controller.groundCollisionMask, QueryTriggerInteraction.Ignore)) return;
        // Validate the riding camera before transferring the inventory item.
        if (controller._firstPersonTransposer == null ||
            controller.FirstPersonCamera == null || controller.FirstPersonPOV == null)
        { BoardItem.Notify("The riding camera is not ready yet."); return; }
        if (!BoardItem.Deploy()) return;
        _parkGround = hit.point;
        _yaw = controller.Rotation.eulerAngles.y;
        _parkRotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, _yaw, 0);
        _boardArea = area;
        _board = BoardItem.CreateModel();
        _parking = new ParkingSpot(area, new(_parkGround.x, _parkGround.y, _parkGround.z),
            new(_parkRotation.x, _parkRotation.y, _parkRotation.z, _parkRotation.w));
        _idle = new RideMotion(Profile);
        _groundMask = controller.groundCollisionMask;
        _parkHeight = Profile.HoverHeight;
        _hasPose = true;
        // Deploy at the player's validated position and mount without a synthetic click.
        BeginRide(player, controller, area);
        _resumeFrame = Time.frameCount; // Consume the inventory/quick-action click.
    }

    [HideFromIl2Cpp]
    private void Mount()
    {
        var player = PlayerManager._instance?.LocalPlayer?.Character;
        var controller = player?.Controller;
        if (player == null || controller == null || !CanRide(player))
        {
            Plugin.Logger.LogInfo("HOBA: stand on foot with empty hands before mounting.");
            return;
        }
        if (controller._firstPersonTransposer == null || controller.FirstPersonCamera == null)
            throw new InvalidOperationException("The first-person camera is not ready.");
        var area = CurrentArea();
        if (string.IsNullOrEmpty(area)) return;
        if (_parking != null && !StepOntoBoard(controller))
        {
            Plugin.Logger.LogInfo("HOBA: move closer on the same walkway to mount.");
            return;
        }
        BeginRide(player, controller, area);
    }

    // Common state transition, reached either directly from a successful item
    // deployment or after the approach checks for an existing parked board.
    [HideFromIl2Cpp]
    private void BeginRide(PlayerCharacter player, PlayerCharacterController controller, string area)
    {
        SprayTool.Unequip();
        ParkingPrompt.Release();
        _player = player;
        _controller = controller;
        _transposer = controller._firstPersonTransposer;
        _camera = controller.FirstPersonCamera;
        _followOffset = _transposer.m_FollowOffset;
        _dutch = _camera.m_Lens.Dutch;
        _headBob = controller.headBobEnabled;
        var pov = controller.FirstPersonPOV;
        _horizontalRecentering = pov.m_HorizontalRecentering.m_enabled;
        _verticalRecentering = pov.m_VerticalRecentering.m_enabled;
        _rideHeight = _parkHeight;
        _lastPosition = controller.transform.position;
        _scene = SceneManager.GetActiveScene().handle;
        _normal = Vector3.up;
        _ground.Reset();
        _pitch.Reset();
        _look.Reset();
        _lookInputFrame = -1;
        _lookPoseReady = false;
        _wasSuspended = false;
        _turnStrength = 0;
        _cameraBank = 0;
        _visualBlend = 0;
        _motion = _idle ?? new RideMotion(Profile); // Remounting retains a coasting board's speed.
        _groundMask = controller.groundCollisionMask;
        _board ??= BoardItem.CreateModel();
        _board.Root.SetActive(true);
        _parking = null;
        _idle = null;
        _boardArea = area;
        _handsLock = controller.HandsAnimator.Hide(this);
        _interactionLock = player.Interaction.DisableInteractions(this);
        controller.headBobEnabled = false;
        var horizontalRecentering = pov.m_HorizontalRecentering;
        horizontalRecentering.m_enabled = false;
        pov.m_HorizontalRecentering = horizontalRecentering;
        var verticalRecentering = pov.m_VerticalRecentering;
        verticalRecentering.m_enabled = false;
        pov.m_VerticalRecentering = verticalRecentering;
        controller.HandsAnimator.footstepAudioSource?.Stop();
        Present(0);
        Plugin.Logger.LogInfo("HOBA-01 mounted: W forward, S reverse, A/D turn; mouse free-looks, Shift crouches, left click parks the board.");
    }

    [HideFromIl2Cpp]
    internal bool Owns(PlayerCharacterController controller) => _motion != null && _controller != null && _controller.Pointer == controller.Pointer;

    [HideFromIl2Cpp]
    internal Vector3 Move(float dt)
    {
        if (!ValidRide()) { Dismount("movement blocked"); return Vector3.zero; }
        var motion = _motion!;
        // Read native bound input before camera-relative projection flattens its forward axis.
        // Camera pitch/yaw must never affect board throttle or steering.
        var input = Nivalis.PlayerInputManager._instance?.Input?.m_GameplayControls_Move?.ReadValue<Vector2>() ?? Vector2.zero;
        var forward = Mathf.Clamp(input.y, -1, 1);
        var turn = Mathf.Clamp(input.x, -1, 1);
        var shift = Keyboard.current?.leftShiftKey.isPressed == true || Keyboard.current?.rightShiftKey.isPressed == true;
        motion.Step(forward, turn, dt, shift);
        _turnStrength = Mathf.Abs(turn) * motion.Crouch * motion.SpeedRatio;
        _yaw = Mathf.Repeat(_yaw + motion.YawDelta, 360);
        return Quaternion.Euler(0, _yaw, 0) * new Vector3(0, 0, motion.Forward);
    }

    [HideFromIl2Cpp]
    internal void AfterMovement(Vector3 before, float dt)
    {
        if (_controller == null || _motion == null) return;
        var traveled = Vector3.ProjectOnPlane(_controller.transform.position - before, Vector3.up).magnitude;
        var requested = Mathf.Abs(_motion.Forward) * dt;
        if (requested > .025f && traveled < requested * .1f) _motion.Blocked();
        _lastPosition = _controller.transform.position;
    }

    [HideFromIl2Cpp]
    internal void Present(float dt)
    {
        if (_motion == null || _controller == null || _board?.Root == null || Suspended) return;
        _visualBlend = Mathf.MoveTowards(_visualBlend, 1, dt * 3);
        _rideHeight = Mathf.MoveTowards(_rideHeight, Profile.HoverHeight, dt * .45f);
        var motion = _motion;
        var controller = _controller;
        var heading = Quaternion.Euler(0, _yaw, 0);
        var ground = controller.transform.position;
        var fallback = controller.CharacterController != null && controller.CharacterController.enabled
            ? controller.CharacterController.bounds.min.y : ground.y;
        float Sample(float x, float z)
        {
            var origin = ground + heading * new Vector3(x, .65f, z);
            return Physics.Raycast(origin, Vector3.down, out var hit, 2.5f,
                controller.groundCollisionMask, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
        }
        // A broad footprint rejects individual paving seams and tiny collision normals.
        var fl = Sample(-.23f, .55f); var fc = Sample(0, .55f); var fr = Sample(.23f, .55f);
        var bl = Sample(-.23f, -.55f); var bc = Sample(0, -.55f); var br = Sample(.23f, -.55f);
        var ml = Sample(-.23f, 0); var mr = Sample(.23f, 0); var mc = Sample(0, 0);
        var front = GroundResponse.Median(fl, fc, fr);
        var back = GroundResponse.Median(bl, bc, br);
        var left = GroundResponse.Median(fl, ml, bl);
        var right = GroundResponse.Median(fr, mr, br);
        _surfaceHeight = GroundResponse.Median((front + back) / 2, (left + right) / 2, mc);
        _ground.Step(_surfaceHeight, (front - back) / 1.1f, (right - left) / .46f, dt);
        ground.y = _ground.Height;
        _normal = heading * new Vector3(-_ground.CrossSlope, 1, -_ground.ForwardSlope).normalized;
        _parkGround = ground + heading * new Vector3(0, 0, .08f);
        _parkRotation = Quaternion.FromToRotation(Vector3.up, _normal) * heading;
        _hasPose = true;
        _board.Root.transform.SetPositionAndRotation(new Vector3(ground.x, Mathf.Max(ground.y, mc - .08f), ground.z) + heading * new Vector3(0, _rideHeight + motion.Hover, .08f),
            Quaternion.FromToRotation(Vector3.up, _normal) * heading * Quaternion.Euler(0, 0, motion.Bank * 1.6f));
        _board.SetScanVisible(false);
        _board.Pulse(motion.Pulse, motion.SpeedRatio, motion.Bank);
        if (_transposer != null)
            _transposer.m_FollowOffset = _followOffset + new Vector3(motion.Sway, _rideHeight + motion.Hover - .45f * motion.Crouch + ground.y - controller.transform.position.y, 0) * _visualBlend;
        if (_camera != null)
        {
            var lens = _camera.m_Lens;
            _cameraBank += (motion.Bank * .45f - _cameraBank) * (1 - Mathf.Exp(-8 * dt));
            lens.Dutch = _dutch + _cameraBank * _visualBlend;
            _camera.m_Lens = lens;
        }
    }

    // UI/input suspension is temporary, unlike losing the player or gameplay scene.
    internal bool Suspended
    {
        [HideFromIl2Cpp]
        get
        {
            if (_motion == null) return false;
            if (!GameplayAvailable()) { _wasSuspended = true; return true; }
            if (_wasSuspended) return true; // Update consumes the resume frame.
            return _resumeFrame == Time.frameCount;
        }
    }

    [HideFromIl2Cpp]
    internal void ConstrainLook(CinemachinePOV pov, float dt)
    {
        if (_motion == null || _controller?.FirstPersonPOV?.Pointer != pov.Pointer) return;
        var horizontal = pov.m_HorizontalAxis;
        var vertical = pov.m_VerticalAxis;
        if (Suspended)
        {
            if (_lookPoseReady)
            {
                horizontal.Value = _lookPose.x;
                vertical.Value = _lookPose.y;
            }
        }
        else
        {
            // Observe actual look input, not camera motion caused by board following.
            // Pitch input also resets the idle timer; native bindings include controllers.
            if (_lookInputFrame != Time.frameCount)
            {
                _lookInputFrame = Time.frameCount;
                var input = _controller.lookAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
                var mouse = Mouse.current?.delta.ReadValue() ?? Vector2.zero;
                _look.ObserveInput(input.sqrMagnitude > .0001f || mouse.sqrMagnitude > .0001f, dt);
            }
            // Stay away from the vertical poles before deriving yaw from the orientation.
            var minimumPitch = Mathf.Max(vertical.m_MinValue, -RidePitch.LookLimit);
            var maximumPitch = Mathf.Min(vertical.m_MaxValue, RidePitch.LookLimit);
            vertical.Value = Mathf.Clamp(vertical.Value, minimumPitch, maximumPitch);
            // Match POV's native orientation construction, including its parent.
            var parent = _camera!.transform.parent;
            var basis = parent != null ? parent.rotation : Quaternion.identity;
            var orientation = basis * Quaternion.Euler(vertical.Value, horizontal.Value, 0);
            var yaw = orientation.eulerAngles.y;
            var target = _look.Step(yaw, _yaw, _turnStrength, dt);
            horizontal.Value += Mathf.DeltaAngle(yaw, target);
            vertical.Value = _pitch.Step(vertical.Value, _ground.ForwardSlope, _look.AutoFollow,
                dt, minimumPitch, maximumPitch);
            _lookPose = new Vector2(horizontal.Value, vertical.Value);
            _lookPoseReady = true;
        }
        pov.m_HorizontalAxis = horizontal;
        pov.m_VerticalAxis = vertical;
    }

    [HideFromIl2Cpp]
    private bool ValidRide() => _player != null && _controller != null &&
        GameSceneManager._instance != null && GameSceneManager._instance.IsGame &&
        !GameSceneManager._instance.IsLoading && !GameSceneManager.IsUnloadingGameplay &&
        PlayerManager._instance?.LocalPlayer?.Character?.Pointer == _player.Pointer &&
        SceneManager.GetActiveScene().handle == _scene && (Suspended || CanRide(_player)) &&
        (_controller.transform.position - _lastPosition).sqrMagnitude < 16;

    [HideFromIl2Cpp]
    internal static bool CanRide(PlayerCharacter player)
    {
        var c = player.Controller;
        var h = c?.HandsAnimator;
        return c != null && h != null && c.CanMove && c.State == PlayerCharacterController.ControllerState.Normal &&
            player.DrivenBoat == null && c.boardedBoat == null && player.CanEquipItem &&
            player.ObjectHolder?.IsHoldingObject != true && !h.IsSitting && !h.IsPlayingMealAnimation && !h.sleeping &&
            !h.placingItem && !h.IsHoldingTray && !h.Snapped && !h.Animator.GetBool(PlayerHandsAnimator.Param.Fishing);
    }

    [HideFromIl2Cpp]
    internal static bool GameplayAvailable()
    {
        if (!Application.isFocused || Time.timeScale <= 0 || PlayerManager._instance?.LocalPlayer?.Character == null) return false;
        var scenes = GameSceneManager._instance;
        if (scenes == null || !scenes.IsGame || scenes.IsLoading || GameSceneManager.IsUnloadingGameplay) return false;
        var ui = UIManager._instance;
        var input = Nivalis.PlayerInputManager._instance;
        if (ui == null || !UIManager.IsVisible || input == null || input.currentRebind != null ||
            input.currentMap == null || !input.currentMap.enabled || input.GameplayControls == null ||
            input.currentMap.Pointer != input.GameplayControls.Pointer) return false;
        var panels = ui._openPanels;
        if (panels == null) return false;
        for (var i = 0; i < panels.Count; i++)
        {
            var panel = panels[i];
            if (panel != null && panel.gameObject.activeInHierarchy && panel.IsVisible &&
                (panel.requiresMouse || panel.TryCast<UIWindow>()?.IsOpen == true)) return false;
        }
        return true;
    }

    [HideFromIl2Cpp]
    internal void Dismount(string reason)
    {
        BoardFov.Reset();
        var wasMounted = _motion != null;
        if (wasMounted && _board?.Root != null && _hasPose)
        {
            _parkGround.y = _surfaceHeight;
            _parking = new ParkingSpot(_boardArea, new(_parkGround.x, _parkGround.y, _parkGround.z),
                new(_parkRotation.x, _parkRotation.y, _parkRotation.z, _parkRotation.w));
            _idle = _motion;
            if (reason != "left click") _idle!.Blocked();
            _parkHeight = _board.Root.transform.position.y - _parkGround.y - _idle!.Hover;
        }
        _motion = null;
        _wasSuspended = false;
        _lookPoseReady = false;
        _turnStrength = 0;
        _look.Reset();
        // Every resource restores independently, including partial startup failures.
        Restore(() => { if (_controller != null) { _controller.headBobEnabled = _headBob; _controller.StopMovement(); } });
        Restore(() =>
        {
            var pov = _controller?.FirstPersonPOV;
            if (pov == null) return;
            var pitch = pov.m_VerticalAxis;
            pitch.Value = Mathf.Clamp(_pitch.WithoutOffset(pitch.Value), pitch.m_MinValue, pitch.m_MaxValue);
            pov.m_VerticalAxis = pitch;
            var horizontal = pov.m_HorizontalRecentering;
            horizontal.m_enabled = _horizontalRecentering;
            pov.m_HorizontalRecentering = horizontal;
            var vertical = pov.m_VerticalRecentering;
            vertical.m_enabled = _verticalRecentering;
            pov.m_VerticalRecentering = vertical;
        });
        Restore(() => { if (_transposer != null) _transposer.m_FollowOffset = _followOffset; });
        Restore(() => { if (_camera != null) { var lens = _camera.m_Lens; lens.Dutch = _dutch; _camera.m_Lens = lens; } });
        _pitch.Reset();
        Restore(() => _handsLock?.Release());
        Restore(() => _interactionLock?.Release());
        _handsLock = _interactionLock = null;
        _player = null; _controller = null; _transposer = null; _camera = null;
        if (wasMounted) Plugin.Logger.LogInfo("HOBA-01 parked: " + reason);
    }

    [HideFromIl2Cpp]
    private static string CurrentArea()
    {
        var scenes = GameSceneManager._instance;
        return scenes == null || !scenes.IsGame || scenes.IsLoading || GameSceneManager.IsUnloadingGameplay
            ? "" : scenes.CurrentGameplaySceneName;
    }

    [HideFromIl2Cpp]
    private void UpdateParked()
    {
        if (_parking == null || _board?.Root == null) return;
        var scenes = GameSceneManager._instance;
        var visible = _parking.IsVisible(CurrentArea(), scenes == null || scenes.IsLoading || GameSceneManager.IsUnloadingGameplay,
            scenes != null && scenes.IsGame);
        if (_board.Root.activeSelf != visible) _board.Root.SetActive(visible);
        if (!visible) { ParkingPrompt.Release(); return; }
        if (Time.timeScale > 0 && Application.isFocused)
        {
            var dt = Mathf.Clamp(Time.deltaTime, 0, .05f);
            _parkHeight = Mathf.MoveTowards(_parkHeight, Profile.UnloadedHeight, dt * .35f);
            var distance = _idle!.Coast(dt);
            if (Mathf.Abs(distance) > .00001f) CoastBoard(distance);
        }
        var p = _parking.Ground;
        var q = _parking.Rotation;
        _board.Root.transform.SetPositionAndRotation(new Vector3(p.X, p.Y + _parkHeight + _idle!.Hover, p.Z),
            new Quaternion(q.X, q.Y, q.Z, q.W) * Quaternion.Euler(0, 0, _idle.Bank * 1.6f));
        _board.SetScanVisible(true);
        _board.Pulse(_idle!.Pulse, _idle.SpeedRatio);
    }

    [HideFromIl2Cpp]
    private void CoastBoard(float distance)
    {
        var heading = Quaternion.Euler(0, _yaw, 0);
        var steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(distance) / .1f));
        var step = heading * Vector3.forward * (distance / steps);
        for (var i = 0; i < steps; i++)
        {
            var target = _parkGround + step;
            // Sample both ends: an unattended board stops before a ledge or steep step.
            if (!Physics.Raycast(target + Vector3.up * .6f, Vector3.down, out var hit, 1.2f,
                    _groundMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - _parkGround.y) > .25f || hit.normal.y < .65f)
            { _idle!.Blocked(); break; }
            var supported = true;
            for (var end = -.65f; end <= .65f; end += 1.3f)
            {
                var probe = target + heading * new Vector3(0, .6f, end);
                if (!Physics.Raycast(probe, Vector3.down, out var support, 1.2f, _groundMask, QueryTriggerInteraction.Ignore) ||
                    Mathf.Abs(support.point.y - hit.point.y) > .4f) { supported = false; break; }
            }
            if (!supported) { _idle!.Blocked(); break; }
            target.y = hit.point.y;
            var displacement = target - _parkGround;
            var center = _parkGround + Vector3.up * (_parkHeight - .07f);
            if (Physics.BoxCast(center, new Vector3(.26f, .15f, .72f), displacement.normalized,
                    out _, _parkRotation, displacement.magnitude + .015f, _groundMask, QueryTriggerInteraction.Ignore))
            { _idle!.Blocked(); break; }
            _parkGround = target;
            _parkRotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * heading;
        }
        _parking = new ParkingSpot(_boardArea, new(_parkGround.x, _parkGround.y, _parkGround.z),
            new(_parkRotation.x, _parkRotation.y, _parkRotation.z, _parkRotation.w));
    }

    [HideFromIl2Cpp]
    internal bool CanReachParked()
    {
        if (_parking == null || _board?.Root == null || !_board.Root.activeSelf || !GameplayAvailable()) return false;
        if (!_parking.IsVisible(CurrentArea(), GameSceneManager._instance.IsLoading, GameSceneManager._instance.IsGame)) return false;
        var player = PlayerManager._instance.LocalPlayer.Character;
        if (!CanRide(player) || player.Interaction.CurrentFocus != null) return false;
        var p = player.transform.position;
        if (!_parking.IsNear(new(p.x, p.y, p.z))) return false;
        var camera = player.Controller.Camera;
        if (camera == null) return false;
        var toBoard = _board.Root.transform.position - camera.transform.position;
        var localOrigin = _board.Root.transform.InverseTransformPoint(camera.transform.position);
        var localDirection = _board.Root.transform.InverseTransformDirection(camera.transform.forward);
        var target = new Bounds(Vector3.zero, new Vector3(.6f, .4f, 1.55f));
        if (!target.IntersectRay(new Ray(localOrigin, localDirection))) return false;
        // A nearby board behind a wall/closed door must not be mountable through it.
        return !Physics.Raycast(camera.transform.position, toBoard.normalized, Mathf.Max(0, toBoard.magnitude - .15f),
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    [HideFromIl2Cpp]
    private bool StepOntoBoard(PlayerCharacterController controller)
    {
        if (!CanReachParked()) return false;
        var target = _parkGround - _parkRotation * new Vector3(0, 0, .08f);
        var delta = Vector3.ProjectOnPlane(target - controller.transform.position, Vector3.up);
        var steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .15f));
        for (var i = 0; i < steps; i++) controller.Move(delta / steps);
        return Vector3.ProjectOnPlane(target - controller.transform.position, Vector3.up).sqrMagnitude <= .04f;
    }

    [HideFromIl2Cpp]
    internal void LeaveArea()
    {
        Dismount("leaving area");
        _idle?.Blocked();
        ParkingPrompt.Release();
        if (_board?.Root != null) _board.Root.SetActive(false);
    }

    [HideFromIl2Cpp]
    internal void ResetBoard(string reason)
    {
        BoardItem.CancelUse();
        SprayTool.Unequip();
        Dismount(reason);
        Restore(ParkingPrompt.Release);
        Restore(() => _board?.Dispose());
        _board = null; _parking = null; _idle = null; _hasPose = false; _boardArea = "";
    }

    [HideFromIl2Cpp]
    private static void Restore(Action action)
    {
        try { action(); } catch (Exception e) { Plugin.Logger.LogWarning("HOBA cleanup: " + e.Message); }
    }

    [HideFromIl2Cpp]
    internal void Fail(Exception e) { Plugin.Logger.LogError(e); ResetBoard("error; normal controls restored"); }
    public void OnDisable() => ResetBoard("component disabled");
    public void OnDestroy() { ResetBoard("component destroyed"); if (Instance == this) Instance = null; }
}
