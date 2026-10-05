using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nivalis;
using Nivalis.InventorySystem;
using Nivalis.CraftingSystem;
using Nivalis.Localization;
using Nivalis.UI;
using Nivalis.Locale.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

internal static class BoardItem
{
    internal const string Id = BoardProducts.LegacyId;
    private const string DeployedInventory = "local.nivalis.hoba.deployed";
    internal static ItemType? Type => Types.GetValueOrDefault(Id);
    internal static readonly Dictionary<string, ItemType> Types = new();
    private static readonly Dictionary<string, LocItemPlain> Texts = new();
    private static float _useUntil, _nextRecovery;
    private static int _useFrame;
    private static IntPtr _usePlayer;
    private static string? _queuedId;
    private static string? _selectedId;
    internal static BoardProduct? Deployed { get; private set; }
    internal static bool IsBoard(ItemType? type) => type != null && Types.ContainsKey(type.Guid) && !BoardProducts.Get(type.Guid).IsPaint;
    internal static bool IsProduct(ItemType? type) => type != null && Types.ContainsKey(type.Guid);
    private static ItemContainer? Inventory => PlayerManager._instance?.LocalPlayer?.Inventory?.Items;
    private static ItemContainer Escrow => InventoriesManager.Instance.GetOrCreateInventory(DeployedInventory, false);

    internal static void Register(ItemDatabase database)
    {
        var template = database._allItems.First(i => i != null && i.IsFishingRod);
        foreach (var product in BoardProducts.All)
        {
            if (Types.ContainsKey(product.Id)) continue;
            var type = Object.Instantiate(template);
            type.name = product.Name;
            type.hideFlags = HideFlags.DontUnloadUnusedAsset;
            type.guid = new SerializableGuid { guid = product.Id };
            type.ArticyGuid = ""; type.storeAchievementId = "";
            type._isFishingRod = false; type.isEquippable = false;
            type.isUseable = true; type.isIngredient = false; type.isPlayerStorable = true;
            type.entityPrefab = null;
            type.tags = new Il2CppSystem.Collections.Generic.List<ObjectTag>();
            type.tagsHashSet = null;
            type.onUseStatChanges = new Il2CppSystem.Collections.Generic.List<ItemType.StatChange>();
            type.allowBuying = product.ForSale;
            // Prevent discovery-only boards entering a vendor buyback inventory.
            type.allowSelling = !product.Legendary;
            type.allowDailyDeal = false;
            type.minStockPerVendor = 1; type.maxStockPerVendor = 1;
            type.basePrice = product.Price; type.baseMarketPrice = product.Price;
            type.decayTimeInDays = 0;
            var textId = "local.nivalis.hoba.product." + product.Id;
            var description = product.IsPaint
                ? "Use to hold a spray can. Aim at a compatible parked board and left-click to repaint it. One can is consumed on success. Use the same paint again to put it away."
                : "Use to deploy and ride. Left click parks; Shift + left click retrieves. " +
                  (product.Legendary ? "Unique graffiti legendary. Found in the world, never sold by dealers." : "Accepts paint from its own HOBA family. Performance is independent of appearance.");
            var text = new LocItemPlain { guid = textId, displayName = product.Name, shortDisplayName = product.Name, description = description };
            type.locObjRef = new LocObjRef<LocItemPlain>(false) { locObjGuid = textId, locObj = text };
            var iconId = product.IsPaint ? "paint-" + product.Finish : product.Model + "-" + product.Finish;
            using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Hoba.Icons." + iconId + "-icon.png")
                ?? throw new InvalidOperationException("Missing HOBA icon: " + iconId);
            using var bytes = new MemoryStream(); stream.CopyTo(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "HOBA.Icon." + iconId, hideFlags = HideFlags.DontUnloadUnusedAsset };
            if (!ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(bytes.ToArray()), false)) throw new InvalidOperationException("Could not load " + iconId);
            texture.filterMode = FilterMode.Point;
            type.icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            type.icon.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Texts[textId] = text;
            Types[product.Id] = type;
        }
        RegisterText();
        foreach (var (id, type) in Types) database._guidItemTypeMap[id] = type;
        database._allItems = new Il2CppReferenceArray<ItemType>(database._allItems.Where(i => i != null && !Types.ContainsKey(i.Guid)).Concat(Types.Values).ToArray());
        Plugin.Logger.LogInfo("Registered 39 HOBA board appearances and 9 single-use paints; 18 dealer products, no legendaries for sale.");
    }

    internal static void RegisterText()
    { foreach (var (id, text) in Texts) LocObjectsDB.Instance.UpdateObject(id, text); }
    internal static void Notify(string message)
    {
        Plugin.Logger.LogInfo("HOBA: " + message);
        if (NotificationManager._instance != null && RideController.Instance != null)
            NotificationManager._instance.CreateMessage("HOBA", message, RideController.Instance);
    }

    private static bool Add(ItemContainer target, ItemInstanceData item)
    {
        var before = target.GetItemCount(item.Type);
        target.TryAdd(item);
        return target.GetItemCount(item.Type) == before + 1;
    }
    private static bool TransferOne(ItemContainer source, ItemContainer target, ItemType type)
    {
        if (source.GetItemCount(type) == 0) return false;
        var item = source.GetStack(type).GetFirstItem();
        if (!Add(target, item)) return false;
        if (source.TryTake(item)) return true;
        if (!target.TryTake(item)) throw new InvalidOperationException("HOBA transfer rollback failed.");
        return false;
    }
    internal static bool Deploy()
    {
        var inventory = Inventory;
        var type = _selectedId != null ? Types.GetValueOrDefault(_selectedId) : null;
        if (inventory != null && (type == null || inventory.GetItemCount(type) == 0))
            type = Types.Values.FirstOrDefault(t => IsBoard(t) && inventory.GetItemCount(t) > 0);
        if (inventory == null || type == null)
        { Notify("Buy a board from your HOBA dealer first."); return false; }
        if (!TransferOne(inventory, Escrow, type)) return false;
        _nextRecovery = 0;
        Deployed = BoardProducts.Get(type.Guid);
        _selectedId = type.Guid;
        return true;
    }
    internal static BoardModel CreateModel()
    {
        var product = Deployed ?? throw new InvalidOperationException("No deployed HOBA inventory instance.");
        return new BoardModel(product.Model!, product.Finish);
    }
    internal static bool Retrieve()
    {
        if (Inventory != null && Deployed != null && TransferOne(Escrow, Inventory, Types[Deployed.Id]))
        { Deployed = null; return true; }
        Notify("Could not put the board away. Make room in your inventory and try again.");
        return false;
    }
    internal static bool GrantLegendary(BoardProduct product)
    {
        var inventory = Inventory;
        if (inventory == null || !product.Legendary || !Types.TryGetValue(product.Id, out var type)) return false;
        var count = inventory.GetItemCount(type);
        inventory.TryCreateInInventory(type, 1, 0);
        return inventory.GetItemCount(type) == count + 1;
    }

    internal static bool IsOwnedStack(ItemStack? stack) => stack != null && stack.StackCount > 0 && Inventory?.ContainsSpecificStack(stack) == true;

    internal static void QueueUse(Component ui, ItemStack? stack)
    {
        if (!IsOwnedStack(stack) || !IsProduct(stack?.Type)) return;
        var product = BoardProducts.Get(stack!.Type.Guid);
        var player = PlayerManager._instance?.LocalPlayer?.Character?.Pointer ?? IntPtr.Zero;
        _queuedId = product.Id;
        _useUntil = Time.unscaledTime + 3; _useFrame = Time.frameCount; _usePlayer = player;
        ui.GetComponentInParent<UIWindow>()?.Close();
    }

    internal static bool HasPaint(BoardProduct paint) => paint.IsPaint && Inventory?.GetItemCount(Types[paint.Id]) > 0;

    internal static bool ApplyDeployedPaint(BoardProduct paint)
    {
        var board = Deployed;
        var inventory = Inventory;
        if (board == null || inventory == null || !HasPaint(paint) || !BoardProducts.CanPaint(board, paint)) return false;
        var source = Escrow;
        var original = Types[board.Id];
        if (source.GetItemCount(original) == 0) return false;
        var stack = source.GetStack(original);
        var item = stack.GetFirstItem();
        var target = Types[BoardProducts.Appearance(board.Model!, paint.Finish).Id];
        var paintItem = inventory.GetStack(Types[paint.Id]).GetFirstItem();
        // Remove from the old native stack before changing its type. The same
        // instance keeps purchase value, creation day and other native metadata.
        var committed = PaintTransaction.Apply(
            () => source.TryTake(stack, item),
            painted => item.type = painted ? target : original,
            () => Add(source, item),
            () => inventory.TryTake(paintItem),
            () => source.TryTake(item),
            () =>
            {
                if (!Add(source, item)) throw new InvalidOperationException("Cannot restore deployed board after paint failure.");
            });
        if (!committed) { Notify("Paint was not applied; the board and paint were retained."); return false; }
        Deployed = BoardProducts.Get(target.Guid);
        if (_selectedId == board.Id) _selectedId = target.Guid;
        Notify("Applied " + paint.Name + " to " + board.Name + ".");
        return true;
    }

    internal static void CancelUse() { _useUntil = 0; _queuedId = null; }
    internal static void BeforeLoad(InventoriesManager manager)
    {
        CancelUse(); SprayTool.Unequip(); _selectedId = null; Deployed = null; _nextRecovery = 0;
        if (manager._inventoriesById != null && manager._inventoriesById.ContainsKey(DeployedInventory)) manager._inventoriesById[DeployedInventory].Clear();
    }
    internal static void Tick(RideController ride)
    {
        if (Type == null || Inventory == null || !HobaSave.Ready) return;
        if (HobaSave.RestorePending)
        {
            HobaSave.RestorePending = false;
            var saved = HobaSave.State.Board;
            if (saved != null && Types.TryGetValue(saved.Product, out var savedType) && Escrow.GetItemCount(savedType) > 0)
            {
                Deployed = BoardProducts.Get(saved.Product);
                try { ride.RestoreParking(saved); _nextRecovery = float.PositiveInfinity; }
                catch { Deployed = null; throw; }
            }
        }
        if (!ride.HasBoard && Time.unscaledTime >= _nextRecovery)
        {
            _nextRecovery = Time.unscaledTime + 3;
            var pending = false;
            var escrow = Escrow;
            foreach (var type in Types.Values)
                while (escrow.GetItemCount(type) > 0)
                    if (!TransferOne(escrow, Inventory, type)) { pending = true; break; }
            if (!pending) _nextRecovery = float.PositiveInfinity;
            Deployed = null;
        }
        if (_useUntil == 0) return;
        if (Time.unscaledTime > _useUntil || PlayerManager._instance?.LocalPlayer?.Character?.Pointer != _usePlayer) { CancelUse(); return; }
        if (Time.frameCount <= _useFrame || !RideController.GameplayAvailable()) return;
        if (_queuedId == null || !Types.TryGetValue(_queuedId, out var requested) || Inventory.GetItemCount(requested) == 0)
        { CancelUse(); Notify("That item is no longer in your inventory."); return; }
        var product = BoardProducts.Get(_queuedId);
        CancelUse();
        if (product.IsPaint) { SprayTool.Equip(product, ride); return; }
        SprayTool.Unequip();
        _selectedId = product.Id;
        ride.UseBoard();
    }
}

[HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.InitializeProviderData))]
internal static class BoardRegistration
{
    private static void Postfix(ItemDatabase __instance)
    { try { BoardItem.Register(__instance); } catch (Exception e) { Plugin.Logger.LogError("HOBA item registration: " + e); } }
}
[HarmonyPatch(typeof(LocObjectsDB), nameof(LocObjectsDB.ForceUpdateCurrentLanguagePairs))]
internal static class BoardTextRegistration { private static void Postfix() => BoardItem.RegisterText(); }

[HarmonyPatch(typeof(InventoryItemDisplayUI), nameof(InventoryItemDisplayUI.HoldButtonListener))]
internal static class BoardInventoryUse
{
    private static bool Prefix(InventoryItemDisplayUI __instance)
    {
        if (!BoardItem.IsProduct(__instance._stack?.Type)) { SprayTool.Unequip(); return true; }
        try { BoardItem.QueueUse(__instance, __instance._stack); }
        catch (Exception e) { Plugin.Logger.LogError("HOBA item use: " + e); BoardItem.Notify("Could not use that item. See the HOBA log."); }
        return false;
    }
}
[HarmonyPatch(typeof(InventoryItemDetailsDisplay), nameof(InventoryItemDetailsDisplay.OnHoldButtonClicked))]
internal static class BoardDetailsUse
{
    private static bool Prefix(InventoryItemDetailsDisplay __instance)
    {
        if (!BoardItem.IsProduct(__instance.Stack?.Type)) { SprayTool.Unequip(); return true; }
        try { BoardItem.QueueUse(__instance, __instance.Stack); }
        catch (Exception e) { Plugin.Logger.LogError("HOBA item use: " + e); BoardItem.Notify("Could not use that item. See the HOBA log."); }
        return false;
    }
}

[HarmonyPatch(typeof(InventoriesManager), nameof(InventoriesManager.InitializeInternal))]
internal static class BoardInventoryLoad
{
    private static void Prefix(InventoriesManager __instance) => BoardItem.BeforeLoad(__instance);
}

// The native visibility predicate recognizes equipment/cameras/consumables, not
// ItemType.isUseable. Reuse its existing button, localization, navigation and
// click listener; only extend visibility for our owned board.
[HarmonyPatch(typeof(InventoryItemDisplayUI), nameof(InventoryItemDisplayUI.RefreshHoldButton))]
internal static class BoardInventoryUseVisibility
{
    private static void Postfix(InventoryItemDisplayUI __instance, ItemStack __0)
    {
        if (!BoardItem.IsProduct(__0?.Type) || __instance.holdButton == null) return;
        __instance.holdButton.gameObject.SetActive(BoardItem.IsOwnedStack(__0));
        // RefreshHoldButton already applied the native interactability restrictions.
    }
}

[HarmonyPatch(typeof(InventoryItemDetailsDisplay), nameof(InventoryItemDetailsDisplay.Refresh))]
internal static class BoardDetailsUseVisibility
{
    private static void Postfix(InventoryItemDetailsDisplay __instance)
    {
        if (!BoardItem.IsProduct(__instance.Stack?.Type) || __instance.holdButton == null) return;
        __instance.holdButton.gameObject.SetActive(BoardItem.IsOwnedStack(__instance.Stack));
    }
}
