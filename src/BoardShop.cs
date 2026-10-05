using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nivalis;
using Nivalis.Economy;
using Nivalis.InventorySystem;
using Nivalis.Localization;
using Nivalis.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

// Native shop/UI, with independent definitions and stock. Never borrow a story person's identity.
internal sealed class BoardShop : IDisposable
{
    private static readonly Dictionary<IntPtr, BoardShop> Shops = new();
    private Character? _character;
    private Vendor? _vendor;
    private VendorType? _type;
    private VendorInteraction? _interaction;
    private BoxCollider? _tradeHitbox;
    private int _fitAfterFrame;
    internal static bool Owns(Vendor? vendor) => vendor != null && Shops.ContainsKey(vendor.Pointer);

    internal static BoardShop Attach(Character character, VendorLocation location)
    {
        var shop = new BoardShop();
        try { shop.Create(character, location); return shop; }
        catch { shop.Dispose(); throw; }
    }
    private void Create(Character character, VendorLocation location)
    {
        _character = character;
        if (BoardItem.Type == null) throw new InvalidOperationException("HOBA item database is not ready.");
        // Native pooled UI prefabs have no assigned shop and canvas is a lazy cache.
        // Select by the actual UI hierarchy, not by those runtime-only fields.
        var candidates = Resources.FindObjectsOfTypeAll<VendorInteraction>();
        var source = candidates.FirstOrDefault(v => v != null && !Owns(v.definition) &&
            v.nameText != null && v.onShopRequested != null &&
            v.nameText.transform.IsChildOf(v.transform) &&
            v.GetComponentInChildren<Canvas>(true) != null &&
            v.GetComponentsInChildren<Character>(true).Length == 0);
        if (source == null)
        {
            var details = string.Join("; ", candidates.Where(v => v != null).Select(v =>
                $"{v.name}: text={v.nameText != null}, event={v.onShopRequested != null}, " +
                $"childCanvas={v.GetComponentInChildren<Canvas>(true) != null}, " +
                $"ownText={v.nameText != null && v.nameText.transform.IsChildOf(v.transform)}"));
            throw new InvalidOperationException("No usable native vendor UI hierarchy. Candidates: " + details);
        }
        var definition = source.definition;
        if (definition == null || definition.type == null || definition.location == null)
            definition = Resources.FindObjectsOfTypeAll<Vendor>().FirstOrDefault(v =>
                v != null && !Owns(v) && v.type != null && v.location != null);
        if (definition == null) throw new InvalidOperationException("Native vendor economy definitions are not ready.");
        Plugin.Logger.LogInfo($"HOBA shop template: {source.name}; economy definition: {definition.name}");
        _vendor = Object.Instantiate(definition);
        _vendor.name = location.Name;
        // A stable per-placement identifier gives the native save system a separate stock container.
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("HOBA.vendor." + location.Id));
        _vendor.guid = new SerializableGuid { guid = new Guid(hash.Take(16).ToArray()).ToString() };
        _vendor.person = null;
        _vendor.storeAchievementId = "";
        _vendor.relationshipLevelProgress = 0;
        _vendor.dailyDeal = new EconomyManager.DailyDeal();
        _type = Object.Instantiate(definition.type);
        var labelId = "HOBA.vendor.label." + location.Id;
        var label = new LocStringSingle { guid = labelId, text = location.Name };
        _type.label = new LocObjRef<LocStringSingle>(false) { locObjGuid = labelId, locObj = label };
        _vendor.type = _type;
        var offers = BoardProducts.All.Where(p => p.ForSale).Select(p => new EconomyManager.VendorItemDefinition
        { type = EconomyManager.BonusType.Item, itemType = BoardItem.Types[p.Id], stockMultiplier = 1, priceBonus = 0 }).ToArray();
        _vendor.offerredItems = new Il2CppReferenceArray<EconomyManager.VendorItemDefinition>(offers);
        _vendor.excludedItems = new Il2CppReferenceArray<ItemType>(BoardProducts.All.Where(p => !p.ForSale).Select(p => BoardItem.Types[p.Id]).ToArray());
        _vendor.items = new Il2CppSystem.Collections.Generic.Dictionary<ItemType, VendorItem>();
        _vendor.container = InventoriesManager.Instance.GetOrCreateInventory("HOBA.shop." + _vendor.Id, false);
        foreach (var offer in offers) _vendor.TryRegisterItem(offer, offer.itemType);
        Shops.Add(_vendor.Pointer, this);
        // A dealer carries one board at a time, replenished when the shop is reopened.
        Restock();
        if (character.Vendor != null) character.Vendor.enabled = false;
        _interaction = Object.Instantiate(source, character.transform, false);
        _interaction.transform.localPosition = Vector3.zero;
        _interaction.transform.localRotation = Quaternion.identity;
        // Resolve the canvas on OUR clone; never keep a donor's cached object.
        _interaction.canvas = _interaction.GetComponentInChildren<Canvas>(true);
        _interaction.name = "HOBA.VendorInteraction." + location.Id;
        _interaction.definition = _vendor; _interaction.initialDefinition = _vendor;
        _interaction.hasCachedInitialDefinition = true;
        _interaction.Init(_vendor);
        character.Vendor = _interaction;
        _interaction.gameObject.SetActive(true);
        _interaction.enabled = true;
        _interaction.IsActive = true;
        _interaction.Initialize();
        _interaction.Show();
        FitToCharacter();
        // Refit once after the animator has established the idle pose.
        _fitAfterFrame = Time.frameCount + 2;
        character.CacheChildrenCanvases();
        RefreshLabel(_interaction);
        Plugin.Logger.LogInfo("HOBA shop ready: " + location.Id + " / " + _vendor.Id);
    }
    internal void FinishPlacement()
    {
        if (_fitAfterFrame == 0 || Time.frameCount < _fitAfterFrame) return;
        _fitAfterFrame = 0;
        FitToCharacter();
    }

    private void FitToCharacter()
    {
        if (_character == null || _interaction == null) return;
        var root = _character.transform;
        var body = new Bounds(root.position + Vector3.up * .85f, new Vector3(.55f, 1.7f, .4f));
        var found = false;
        foreach (var renderer in _character.GetComponentsInChildren<Renderer>(true))
        {
            // Do not measure floating UI, particles, or the display board as part of the person.
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                renderer.transform.IsChildOf(_interaction.transform) ||
                (renderer.TryCast<SkinnedMeshRenderer>() == null && renderer.TryCast<MeshRenderer>() == null) ||
                renderer.GetComponentInParent<Canvas>() != null) continue;
            var bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude < .0001f) continue;
            if (!found) { body = bounds; found = true; }
            else body.Encapsulate(bounds);
        }

        if (_tradeHitbox == null)
        {
            // The native focus raycaster finds IInteractable on the hit object itself.
            // Keep its collision layer, but discard the donor's offset/shape entirely.
            var colliders = _interaction.GetComponentsInChildren<Collider>(true);
            var donor = colliders.FirstOrDefault(c => c.enabled) ?? colliders.FirstOrDefault();
            if (donor != null) _interaction.gameObject.layer = donor.gameObject.layer;
            foreach (var collider in colliders) collider.enabled = false;
            _tradeHitbox = _interaction.gameObject.AddComponent<BoxCollider>();
            _tradeHitbox.isTrigger = true;
        }

        var canvas = _interaction.canvas.transform;
        // Usually the label has its own canvas. If the canvas IS the interaction root,
        // move only the text so a label/billboard offset never drags the trade target along.
        var labelRoot = canvas == _interaction.transform ? _interaction.nameText.transform : canvas;
        var labelBottom = Mathf.Max(root.position.y + 1.9f, body.max.y) + .25f;
        var position = labelRoot.position;
        position.x = body.center.x; position.z = body.center.z;
        labelRoot.position = position;
        var corners = new Il2CppStructArray<Vector3>(4);
        _interaction.nameText.rectTransform.GetWorldCorners(corners);
        var bottom = corners.Min(c => c.y);
        labelRoot.position += Vector3.up * (labelBottom - bottom);

        // Convert all eight world-space corners: non-zero model pivots, rotation and
        // non-unit NPC scale must not leave a translated or undersized click target.
        var local = new Bounds(_interaction.transform.InverseTransformPoint(body.center), Vector3.zero);
        var min = body.min - new Vector3(.06f, .02f, .06f);
        var max = body.max + new Vector3(.06f, .02f, .06f);
        for (var i = 0; i < 8; i++)
            local.Encapsulate(_interaction.transform.InverseTransformPoint(new Vector3(
                (i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z)));
        _tradeHitbox.center = local.center;
        _tradeHitbox.size = local.size;
        _tradeHitbox.enabled = true;
        Plugin.Logger.LogInfo($"HOBA dealer fitted: body {body}, label bottom {labelBottom:F2}, trade bounds {_tradeHitbox.bounds}");
    }

    private void Restock()
    {
        if (_vendor == null) return;
        foreach (var product in BoardProducts.All.Where(p => p.ForSale))
        {
            var type = BoardItem.Types[product.Id];
            if (_vendor.container.GetItemCount(type) == 0)
                _vendor.container.TryCreateInInventory(type, 1, product.Price);
        }
    }
    internal static bool Open(VendorInteraction interaction)
    {
        if (interaction.definition == null || !Shops.TryGetValue(interaction.definition.Pointer, out var shop)) return false;
        shop.Restock();
        // The normal native request opens the full shop and keeps its trade/money checks.
        // DoInteraction also marks a story Person as met; this dealer has no story Person.
        var ui = Resources.FindObjectsOfTypeAll<ShopUINew>().FirstOrDefault(s => s != null && s.gameObject.scene.IsValid());
        if (ui == null) throw new InvalidOperationException("Native shop window is not loaded.");
        ui.Vendor_OnShopRequested(shop._vendor);
        return true;
    }
    internal static void RefreshLabel(VendorInteraction interaction)
    {
        if (!Owns(interaction.definition)) return;
        if (interaction.nameText != null)
        { interaction.nameText.text = interaction.definition.name; interaction.nameText.enabled = true; }
        if (interaction.tierText != null) interaction.tierText.gameObject.SetActive(false);
    }
    public void Dispose()
    {
        if (_vendor != null)
        {
            // Close a shop before destroying the definition it is displaying.
            foreach (var ui in Resources.FindObjectsOfTypeAll<ShopUINew>())
                if (ui._targetVendor == _vendor && ui.gameObject.activeInHierarchy) ui.Close();
            Shops.Remove(_vendor.Pointer);
        }
        if (_character != null && _character.Vendor == _interaction) _character.Vendor = null;
        if (_interaction != null) Object.Destroy(_interaction.gameObject);
        if (_vendor != null) Object.Destroy(_vendor);
        if (_type != null) Object.Destroy(_type);
        _interaction = null; _vendor = null; _type = null;
    }
}

[HarmonyPatch(typeof(VendorInteraction), nameof(VendorInteraction.DoInteraction))]
internal static class BoardShopInteraction
{
    private static bool Prefix(VendorInteraction __instance)
    {
        if (!BoardShop.Owns(__instance.definition)) return true;
        try { BoardShop.Open(__instance); }
        catch (Exception e) { Plugin.Logger.LogError("HOBA shop open: " + e); BoardItem.Notify("Shop could not open. See the HOBA log."); }
        return false;
    }
}
[HarmonyPatch(typeof(VendorInteraction), nameof(VendorInteraction.Initialize))]
internal static class BoardShopLabel
{
    // Native Initialize reads story/localization fields absent on our custom dealer.
    // Its only work is label/tier setup, which RefreshLabel supplies for this dealer.
    private static bool Prefix(VendorInteraction __instance) => !BoardShop.Owns(__instance.definition);
    private static void Postfix(VendorInteraction __instance) => BoardShop.RefreshLabel(__instance);
}

// These reference-only entrypoints are safe IL2CPP hooks. Never detour the native
// ref ShopTradeRequest methods: their value-type wrappers cannot cross that bridge.
[HarmonyPatch(typeof(EconomyManager), nameof(EconomyManager.RegisterInteraction))]
internal static class BoardShopNoStoryInteraction { private static bool Prefix(Vendor __0) => !BoardShop.Owns(__0); }
[HarmonyPatch(typeof(EconomyManager), nameof(EconomyManager.HandleRelationshipOnTransaction))]
internal static class BoardShopNoStoryRelationship { private static bool Prefix(Vendor __0) => !BoardShop.Owns(__0); }

[HarmonyPatch(typeof(VendorInfoPanelUI), nameof(VendorInfoPanelUI.Initialize))]
internal static class BoardShopHeader
{
    private static bool Prefix(VendorInfoPanelUI __instance, Vendor __0)
    {
        if (!BoardShop.Owns(__0)) return true;
        __instance._targetVendor = __0;
        if (__instance.titleLabel != null) __instance.titleLabel.text = __0.name;
        if (__instance.nameLabel != null) __instance.nameLabel.text = "Hoverboards";
        if (__instance.businessLabel != null) __instance.businessLabel.text = "";
        if (__instance.portraitImage != null) __instance.portraitImage.sprite = BoardItem.Type?.icon;
        return false;
    }
}
[HarmonyPatch(typeof(VendorInfoPanelUI), nameof(VendorInfoPanelUI.UpdateBusiness))]
internal static class BoardShopHeaderBusiness
{
    private static bool Prefix(VendorInfoPanelUI __instance) => !BoardShop.Owns(__instance._targetVendor);
}
