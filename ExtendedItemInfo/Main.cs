using HarmonyLib;
using SRML;
using SRML.Console;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
using AssetsLib;
using TMPro;
using UnityEngine.UI;
using InControl;
using UnityEngine.EventSystems;
using System.Linq;
using SRML.Config.Attributes;
using SRML.SR;
using Console = SRML.Console.Console;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace ExtendedItemInfo
{
    public class Main : ModEntryPoint
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{Environment.CurrentDirectory}\\SRML\\Mods\\{modName}";
        public static PlayerAction infoKey;
        public static Sprite headerImage = TextureUtils.LoadImage("menuHeader.png").CreateSprite();
        public static List<Func<Texture2D, Identifiable.Id?>> AdditionalImageMatchers = new List<Func<Texture2D, Identifiable.Id?>>();
        public static List<Func<Identifiable.Id, List<InformationTab>>> AdditionalItemInformation = new List<Func<Identifiable.Id, List<InformationTab>>>();
        public static readonly Identifiable.Id[] vanillaIds = Enum.GetValues(typeof(Identifiable.Id)).Cast<Identifiable.Id>().ToArray();
        public static Dictionary<SRMod, List<Identifiable.Id>> claimedIds = new Dictionary<SRMod, List<Identifiable.Id>>();
        public static Dictionary<SlimeDiet, Func<IEnumerable<Identifiable.Id>>> CanEatOverrides = new Dictionary<SlimeDiet, Func<IEnumerable<Identifiable.Id>>>();
        public static Dictionary<SlimeDiet, Func<IEnumerable<Identifiable.Id>>> CanProduceOverrides = new Dictionary<SlimeDiet, Func<IEnumerable<Identifiable.Id>>>();
        public static Dictionary<SlimeDiet, Func<IEnumerable<Identifiable.Id>>> FavouriteFoodOverrides = new Dictionary<SlimeDiet, Func<IEnumerable<Identifiable.Id>>>();
        public static MarketVariationGetter GetMarketVariation = delegate { };

        public Main()
        {
            var h = new Harmony("com.Aidanamite.ExtendedItemInfo");
            h.Patch(AccessTools.Method(typeof(EnumPatcher), "AddEnumValue", new[] { typeof(Type), typeof(object), typeof(string) }),postfix: new HarmonyMethod(typeof(Main),nameof(EnumPostfix)));
        }

        static void EnumPostfix(Type enumType, string name)
        {
            if (enumType == typeof(Identifiable.Id) && Enum.TryParse<Identifiable.Id>(name,out var id))
            {
                var t = new StackTrace(1);
                SRMod m = null;
                foreach (var f in t.GetFrames())
                {
                    m = SRModLoader.GetModForAssembly(f.GetMethod().DeclaringType.Assembly);
                    if (m != null)
                        break;
                }
                if (m != null)
                    ClaimId(m, id);
            }
        }

        public override void PreLoad()
        {
            (infoKey = BindingRegistry.RegisterBindedAction("key.ExtendedItemInfo.showInfo")).AddDefaultBinding(Key.F5);
            TranslationPatcher.AddUITranslation("key.key.extendediteminfo.showinfo", "Show Item Info");
            HarmonyInstance.PatchAll();
            Object.DontDestroyOnLoad(new GameObject(typeof(KeyHandler).FullName, typeof(KeyHandler)));
            TranslationPatcher.AddUITranslation("t.informationUI.main", "Main");
            TranslationPatcher.AddUITranslation("t.informationUI", "{0}");
            TranslationPatcher.AddUITranslation("t.informationUI.basic.name", "Basic Info");
            TranslationPatcher.AddUITranslation("t.informationUI.basic.desc", "The sort of information most items have");
            TranslationPatcher.AddUITranslation("t.informationUI.basic", "Display Name: {0}\nId Name: {1}\nAdded by {2}\nCan store in vac slots: {3}\nCan store in Silo: {4}\nCan store in Auto-Feeder: {5}\nCan store in Refinery: {6}\nCan be grown: {7}");
            TranslationPatcher.AddUITranslation("t.informationUI.basic_class", "Item Classes:");
            TranslationPatcher.AddUITranslation("t.informationUI.prefab.name", "Prefab");
            TranslationPatcher.AddUITranslation("t.informationUI.prefab.desc", "Information on the prefab connected to the item's id");
            TranslationPatcher.AddUITranslation("t.informationUI.prefab", "Object Scale: {0}\nVacuum Size: {1}\nSupports Fashion Pods: {2}");
            TranslationPatcher.AddUITranslation("t.informationUI.food.name", "Food Values");
            TranslationPatcher.AddUITranslation("t.informationUI.food.desc", "Information about the food properties of the item");
            TranslationPatcher.AddUITranslation("t.informationUI.food_group", "Food Groups:");
            TranslationPatcher.AddUITranslation("t.informationUI.food_eat", "Eaten By:");
            TranslationPatcher.AddUITranslation("t.informationUI.food_produce", "Produced By:");
            TranslationPatcher.AddUITranslation("t.informationUI.food_favourite", "Favourite Of:");
            TranslationPatcher.AddUITranslation("t.informationUI.slime.name", "Slime Info");
            TranslationPatcher.AddUITranslation("t.informationUI.slime.desc", "Information about the slime properties of the item");
            TranslationPatcher.AddUITranslation("t.informationUI.slime", "Can Largofy: {0}\nIs Largo: {1}");
            TranslationPatcher.AddUITranslation("t.informationUI.slime_toy", "Favourite Toys:");
            TranslationPatcher.AddUITranslation("t.informationUI.slime_base", "Base Slimes:");
            TranslationPatcher.AddUITranslation("t.informationUI.slime_appearance", "Appearances:");
            TranslationPatcher.AddUITranslation("t.informationUI.slime_eats", "Eats:");
            TranslationPatcher.AddUITranslation("t.informationUI.slime_produces", "Produces:");
            TranslationPatcher.AddUITranslation("t.informationUI.slime_favourite", "Favourites:");
            TranslationPatcher.AddUITranslation("t.informationUI.price.name", "Market Info");
            TranslationPatcher.AddUITranslation("t.informationUI.price.desc", "Information about the market value of the item");
            TranslationPatcher.AddUITranslation("t.informationUI.price", "Max Price: {0}\nCurrent Price: {1}\nMin Price: {2}\nPrevious Price: {3}\nMax Market Saturation: {4}");
            TranslationPatcher.AddUITranslation("t.informationUI.recipe.name", "Crafting Info");
            TranslationPatcher.AddUITranslation("t.informationUI.recipe.desc", "The gadget recipes that the item can be used in");
            TranslationPatcher.AddUITranslation("t.informationUI.recipe", "Amount in storage: {0}");
            TranslationPatcher.AddUITranslation("l.foodgroup.fruit", "Fruit");
            TranslationPatcher.AddUITranslation("l.foodgroup.veggies", "Veggies");
            TranslationPatcher.AddUITranslation("l.foodgroup.meat", "Meat");
            TranslationPatcher.AddUITranslation("l.foodgroup.nontarrgold_slimes", "Slimes");
            TranslationPatcher.AddUITranslation("l.foodgroup.plorts", "Plorts");
            TranslationPatcher.AddUITranslation("l.foodgroup.ginger", "Ginger");
        }
        public override void Load()
        {
            foreach (var m in SRModLoader.Mods.Values)
                foreach (Type type in m.EntryType.Assembly.GetTypes())
                    if (type.GetCustomAttributes(true).Any((object x) => x is SRML.Utils.Enum.EnumHolderAttribute))
                        foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                            if (fieldInfo.FieldType == typeof(Identifiable.Id))
                                ClaimId(m, (Identifiable.Id)fieldInfo.GetValue(null));
        }
        public static void Log(string message) => Debug.Log($"[{modName}]: " + message);

        public static void ClaimId(Identifiable.Id id)
        {
            var mod = SRModLoader.GetModForAssembly(Assembly.GetCallingAssembly());
            ClaimId(mod,id);
        }
        public static void ClaimId(SRMod mod, Identifiable.Id id)
        {
            if (!claimedIds.TryGetValue(mod, out var ids))
                ids = claimedIds[mod] = new List<Identifiable.Id>();
            ids.AddUnique(id);
        }
    }
    public delegate void MarketVariationGetter(ref float max1, ref float min1, ref float max2, ref float min2);
    public class KeyHandler : MonoBehaviour
    {
        bool last = false;
        void Start() => StartCoroutine(UpdateCoroutine());
        IEnumerator UpdateCoroutine()
        {
            while (true)
            {
                foreach (var b in Main.infoKey.Bindings)
                {
                    foreach (var d in InputManager.ActiveDevices)
                        UpdateState(b.GetState(d));
                    UpdateState(b.GetState(InputManager.ActiveDevice));
                }
                yield return null;
            }
        }
        void UpdateState(bool state)
        {
            if (state == last)
                return;
            last = state;
            if (state)
            {
                Identifiable.Id? nID = null;
                foreach (var e in ItemDetector.entered)
                {
                    var i = e.Id;
                    if (i != null && i.Value != Identifiable.Id.NONE)
                    {
                        nID = i;
                        break;
                    }
                }
                if ((nID == null || nID.Value == Identifiable.Id.NONE) && SceneContext.Instance.PlayerState.Targeting)
                {
                    var r = Identifiable.GetId(SceneContext.Instance.PlayerState.Targeting);
                    if (r == Identifiable.Id.NONE)
                        r = SceneContext.Instance.PlayerState.Targeting.GetComponent<GordoIdentifiable>()?.id ?? Identifiable.Id.NONE;
                    if (r != Identifiable.Id.NONE)
                        nID = r;
                }
                if (nID == null || nID.Value == Identifiable.Id.NONE)
                {
                    Patch_StorageSlotUI_Awake.slotRaycasters.RemoveAll(x => !x);
                    var p = new PointerEventData(EventSystem.current);
                    p.position = Input.mousePosition;
                    foreach (var e in Patch_StorageSlotUI_Awake.slotRaycasters)
                    {
                        var rays = new List<RaycastResult>();
                        e.Raycast(p, rays);
                        foreach (var r in rays)
                        {
                            var i = r.gameObject.GetComponent<ItemDetector>()?.Id;
                            if (i != null && i.Value != Identifiable.Id.NONE)
                            {
                                nID = i;
                                break;
                            }
                        }
                    }
                }
                if ((nID == null || nID.Value == Identifiable.Id.NONE))
                {
                    var r = SceneContext.Instance.PlayerState.Ammo.GetSelectedId();
                    if (r != Identifiable.Id.NONE)
                        nID = r;
                }
                if (nID != null)
                {
                    var id = nID.Value;
                    var m = Main.claimedIds.FirstOrDefault(x => x.Value.Contains(id)).Key;
                    var p = GetPrefab(id);
                    var c = GetClasses(id);
                    if (GameContext.Instance.LookupDirector.gordoDict.TryGetValue(id, out var gord) && SceneContext.Instance.PlayerState.Targeting?.GetComponent<GordoEat>())
                        gord = SceneContext.Instance.PlayerState.Targeting;
                    var items = new List<InformationItem>()
                    {
                        new InformationItem("t.informationUI.basic.name","t.informationUI.basic.desc",
                            (GameContext.Instance.LookupDirector.toyDict.TryGetValue(id,out var toy) 
                                ? toy.Icon
                                : gord?.GetComponent<GordoDisplayOnMap>()?.markerPrefab?.GetComponent<Image>()?.sprite
                                    ? gord.GetComponent<GordoDisplayOnMap>().markerPrefab.GetComponent<Image>().sprite
                                    : GameContext.Instance.LookupDirector.GetIcon(id))
                                ?? SceneContext.Instance.PediaDirector.lockedEntry.icon,
                            new[] {
                                ContentItem.CreateNormalItem(
                                    "t.informationUI.basic",
                                    Identifiable.GetName(id,false)??"*Display Name Not Found*",
                                    id,
                                    m == null?(Main.vanillaIds.Contains(id)?"the base game":"an unknown mod"):$"the mod {m.ModInfo.Name} ({m.ModInfo.Id})",
                                    SceneContext.Instance.PlayerState.ammoDict.GetAll(x => x.Value != null && x.Value.potentialAmmo.Contains(id)).Cast(x => (x.Key,x.For(z => 0,(z,y) => y >= z.Value.slotPreds.Length,(z,y) => y + 1,(z,y) => y,(z,y) => z.Value.slotPreds[y]?.Invoke(id)??false))).Join(x => "\n • " + x.Key + ": " + x.Item2.Join()),
                                    GameContext.Instance.LookupDirector.GetPlotPrefab(LandPlot.Id.SILO).GetComponentInChildren<SiloStorage>(true).type.GetContents().Contains(id),
                                    GameContext.Instance.LookupDirector.GetPlotPrefab(LandPlot.Id.CORRAL).GetComponentInChildren<SlimeFeeder>(true).GetComponentInParent<SiloStorage>(true).type.GetContents().Contains(id),
                                    GadgetDirector.IsRefineryResource(id),
                                    GameContext.Instance.LookupDirector.GetPlotPrefab(LandPlot.Id.GARDEN).GetComponentInChildren<GardenCatcher>(true).plantable.Any(x => x.id == id)
                                ),
                                ContentItem.CreateNormalItem("t.informationUI", " "),
                                ContentItem.CreateHeaderItem("t.informationUI.basic_class"),
                                ContentItem.CreateNormalItem("t.informationUI", c.Join(x => " • " + x, "\n"))
                            }
                        )
                    };
                    if (p != null)
                        items.Add(new InformationItem("t.informationUI.prefab.name", "t.informationUI.prefab.desc", SceneContext.Instance.PediaDirector.entryDict[PediaDirector.Id.BASICS].icon, new[] {
                            ContentItem.CreateNormalItem(
                                "t.informationUI.prefab",
                                p.transform.localScale.x,
                                p.GetComponent<Vacuumable>()?.size.ToString() ?? "UNSET",
                                p.GetComponent<AttachFashions>() != null
                            )
                        }));
                    var foodContent = new List<ContentItem>();
                    var foods = new List<SlimeEat.FoodGroup>();
                    foreach (var f in SlimeEat.foodGroupIds)
                        if (f.Value != null && f.Value.Contains(id))
                            foods.Add(f.Key);
                    var bundle = GameContext.Instance.MessageDirector.GetBundle("ui");
                    string GetName(SlimeEat.FoodGroup group)
                    {
                        var key = "l.foodgroup." + group.ToString().ToLowerInvariant();
                        var r = bundle.Get(key, false);
                        if (key == r)
                            return group.ToString();
                        return r;
                    }
                    if (foods.Count > 0)
                        foodContent.AddRange(new[]
                        {
                            ContentItem.CreateHeaderItem("t.informationUI.food_group"),
                            ContentItem.CreateNormalItem("t.informationUI",foods.Join(x => " • " + GetName(x), "\n"))
                        });
                    var eaten = new List<Identifiable.Id>();
                    var produced = new List<Identifiable.Id>();
                    var favourite = new List<Identifiable.Id>();
                    foreach (var d in GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable)
                        if (d.Value != null)
                        {
                            if (!Config.IncludeLargosInEatInfo && (d.Value.IsLargo || Identifiable.IsLargo(d.Key)))
                                continue;
                            if (DietAllEats(d.Value).Contains(id))
                                eaten.Add(d.Key);
                            if (DietAllProduces(d.Value).Contains(id))
                                produced.Add(d.Key);
                            if (DietAllFavourites(d.Value).Contains(id))
                                favourite.Add(d.Key);
                        }
                    if (eaten.Count > 0)
                    {
                        if (foodContent.Count > 0)
                            foodContent.Add(ContentItem.CreateNormalItem("t.informationUI", " "));
                        foodContent.AddRange(new[]
                        {
                            ContentItem.CreateHeaderItem("t.informationUI.food_eat"),
                            ContentItem.CreateNormalItem("t.informationUI",eaten.Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*"), "\n"))
                        });
                    }
                    if (produced.Count > 0)
                    {
                        if (foodContent.Count > 0)
                            foodContent.Add(ContentItem.CreateNormalItem("t.informationUI", " "));
                        foodContent.AddRange(new[]
                        {
                            ContentItem.CreateHeaderItem("t.informationUI.food_produce"),
                            ContentItem.CreateNormalItem("t.informationUI",produced.Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*"), "\n"))
                        });
                    }
                    if (favourite.Count > 0)
                    {
                        if (foodContent.Count > 0)
                            foodContent.Add(ContentItem.CreateNormalItem("t.informationUI", " "));
                        foodContent.AddRange(new[]
                        {
                            ContentItem.CreateHeaderItem("t.informationUI.food_favourite"),
                            ContentItem.CreateNormalItem("t.informationUI",favourite.Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*"), "\n"))
                        });
                    }
                    if (foodContent.Count > 0)
                        items.Add(new InformationItem("t.informationUI.food.name", "t.informationUI.food.desc", GameContext.Instance.LookupDirector.GetIcon(Identifiable.Id.SPICY_TOFU), foodContent.ToArray()));
                    bundle = GameContext.Instance.MessageDirector.GetBundle("actor");
                    if (GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable.TryGetValue(id, out var def))
                        items.Add(new InformationItem("t.informationUI.slime.name", "t.informationUI.slime.desc", SceneContext.Instance.PediaDirector.entryDict[PediaDirector.Id.CORRAL].icon, new[] {
                            ContentItem.CreateNormalItem("t.informationUI.slime",def.CanLargofy,def.IsLargo),
                            ContentItem.CreateNormalItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_toy"),
                            ContentItem.CreateNormalItem("t.informationUI",def.FavoriteToys == null ? "" : def.FavoriteToys.Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*"), "\n")),
                            ContentItem.CreateNormalItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_base"),
                            ContentItem.CreateNormalItem("t.informationUI",def.BaseSlimes == null ? "" : def.BaseSlimes.Join(x => " • " + (Identifiable.GetName(x.IdentifiableId,false)??$"*Id: {x.IdentifiableId}*"), "\n")),
                            ContentItem.CreateHeaderItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_appearance"),
                            ContentItem.CreateNormalItem("t.informationUI",def.Appearances.Join(x => " • " + (x==null ? "*missing*" : $"{x.SaveSet} => {bundle.Get(x.NameXlateKey)}") , "\n")),
                            ContentItem.CreateHeaderItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_favourite"),
                            ContentItem.CreateNormalItem("t.informationUI",DietAllFavourites(def).Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*") , "\n")),
                            ContentItem.CreateHeaderItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_eats"),
                            ContentItem.CreateNormalItem("t.informationUI",DietAllEats(def).Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*") , "\n")),
                            ContentItem.CreateHeaderItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_produces"),
                            ContentItem.CreateNormalItem("t.informationUI",DietAllProduces(def).Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*") , "\n"))
                        }));
                    else if (gord?.GetComponent<GordoEat>())
                        items.Add(new InformationItem("t.informationUI.slime.name", "t.informationUI.slime.desc", SceneContext.Instance.PediaDirector.entryDict[PediaDirector.Id.GORDO_SLIME].icon, new[] {
                            ContentItem.CreateHeaderItem("t.informationUI.slime_favourite"),
                            ContentItem.CreateNormalItem("t.informationUI",DietAllFavourites(gord.GetComponent<GordoEat>().slimeDefinition).Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*") , "\n")),
                            ContentItem.CreateHeaderItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_eats"),
                            ContentItem.CreateNormalItem("t.informationUI",DietAllEats(gord.GetComponent<GordoEat>().slimeDefinition).Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*") , "\n")),
                            ContentItem.CreateHeaderItem("t.informationUI", " "),
                            ContentItem.CreateHeaderItem("t.informationUI.slime_produces"),
                            ContentItem.CreateNormalItem("t.informationUI",GetAllProduces(gord.GetComponent<GordoRewardsBase>()).Join(x => " • " + (Identifiable.GetName(x,false)??$"*Id: {x}*") , "\n"))
                        }));
                    if (SceneContext.Instance.EconomyDirector.currValueMap.TryGetValue(id, out var value) && value != null)
                    {
                        (var mx1, var mn1, var mx2, var mn2) = (1.3f, 0.7f, 1.3f, 0.7f);
                        Main.GetMarketVariation(ref mx1, ref mn1, ref mx2, ref mn2);
                        items.Add(new InformationItem("t.informationUI.price.name", "t.informationUI.price.desc", SceneContext.Instance.PediaDirector.entryDict[PediaDirector.Id.PLORT_MARKET].icon, new[] {
                            ContentItem.CreateNormalItem("t.informationUI.price",Mathf.RoundToInt(value.baseValue * 2 * mx1 * mx2),Mathf.RoundToInt(value.currValue),Mathf.RoundToInt(value.baseValue * mn1 * mn2),Mathf.RoundToInt(value.prevValue),value.fullSaturation)
                        }));
                    }
                    var recipes = GameContext.Instance.LookupDirector.gadgetDefinitionDict.GetAll(x => x.Value?.craftCosts?.Any(y => y.id == id) ?? false);
                    if (recipes.Count > 0) {
                        var content = new List<ContentItem>() { ContentItem.CreateNormalItem("t.informationUI.recipe",SceneContext.Instance.GadgetDirector.GetRefineryCount(id)) };
                        foreach (var i in recipes)
                            content.AddRange(new[] {
                                ContentItem.CreateHeaderItem("t.informationUI", " "),
                                ContentItem.CreateHeaderItem("t.informationUI",Gadget.GetName(i.Key,false)??$"*Id: {i.Key}*"),
                                ContentItem.CreateNormalItem("t.informationUI",i.Value.craftCosts.Join(x => " • " + x.amount + " x " + (Identifiable.GetName(x.id,false)??$"*Id: {x}*") , "\n"))
                            });
                        items.Add(new InformationItem("t.informationUI.recipe.name", "t.informationUI.recipe.desc", SceneContext.Instance.PediaDirector.entryDict[PediaDirector.Id.REFINERY].icon, content.ToArray()));
                    }
                    var tabs = new List<InformationTab>()
                    {
                        new InformationTab("t.informationUI.main", items.ToArray())
                    };
                    foreach (var f in Main.AdditionalItemInformation)
                    {
                        List<InformationTab> l;
                        try
                        {
                            l = f?.Invoke(nID.Value);
                        } catch (Exception e)
                        {
                            Debug.LogError("An error occured while fetching additional item information\n" + e);
                            continue;
                        }
                        if (l != null)
                            tabs.AddRange(l);
                    }
                    if (tabs.Count == 1)
                        tabs[0].Available = () => false;
                    UIUtils.CreateInformationUI(Main.headerImage, tabs);
                }
                else
                    Main.Log("No target found");
            }
        }
        public static GameObject GetPrefab(Identifiable.Id id)
        {
            if (GameContext.Instance.LookupDirector.identifiablePrefabDict.TryGetValue(id, out var g) && g != null)
                return g;
            if (GameContext.Instance.LookupDirector.gordoDict.TryGetValue(id, out g) && g != null)
                return g;
            return null;
        }
        public static List<string> GetClasses(Identifiable.Id id)
        {
            var l = new List<string>();
            foreach (var f in typeof(Identifiable).GetFields((BindingFlags)(-1)))
                if (f.IsStatic && f.Name.EndsWith("_CLASS"))
                {
                    var i = f.GetValue(null) as IEnumerable<Identifiable.Id>;
                    if (i != null && i.Contains(id))
                        l.Add(f.Name.Remove(f.Name.Length - 6));
                }
            return l;
        }
        public static List<Identifiable.Id> DietAllEats(SlimeDefinition def)
        {
            if (Main.CanEatOverrides.TryGetValue(def.Diet, out var f))
                return f?.Invoke()?.ToList() ?? new List<Identifiable.Id>();
            var l = new List<Identifiable.Id>();
            def.Diet.RefreshEatMap(GameContext.Instance.SlimeDefinitions, def);
            if (def.Diet.EatMap != null)
                foreach (var e in def.Diet.EatMap)
                    if (e.eats != Identifiable.Id.NONE && e.becomesId != Identifiable.Id.TARR_SLIME && (Config.IncludeLargosInEatInfo || !(GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable.TryGetValue(e.eats, out var d) && d && (d.IsLargo || Identifiable.IsLargo(d.IdentifiableId))) || !(e.becomesId != Identifiable.Id.NONE && GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable.TryGetValue(e.becomesId, out d) && d && (d.IsLargo || Identifiable.IsLargo(d.IdentifiableId)))))
                        l.AddUnique(e.eats);
            return l;
        }
        public static List<Identifiable.Id> DietAllProduces(SlimeDefinition def)
        {
            if (Main.CanProduceOverrides.TryGetValue(def.Diet, out var f))
                return f?.Invoke()?.ToList() ?? new List<Identifiable.Id>();
            var l = new List<Identifiable.Id>();
            def.Diet.RefreshEatMap(GameContext.Instance.SlimeDefinitions, def);
            if (def.Diet.EatMap != null)
                foreach (var e in def.Diet.EatMap)
                    if (e.producesId != Identifiable.Id.NONE && (Config.IncludeLargosInEatInfo || !(GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable.TryGetValue(e.producesId, out var d) && d && (d.IsLargo || Identifiable.IsLargo(d.IdentifiableId)))))
                        l.AddUnique(e.producesId);
            return l;
        }
        public static List<Identifiable.Id> DietAllFavourites(SlimeDefinition def)
        {
            if (Main.FavouriteFoodOverrides.TryGetValue(def.Diet, out var f))
                return f?.Invoke()?.ToList() ?? new List<Identifiable.Id>();
            if (Main.CanEatOverrides.TryGetValue(def.Diet, out _))
                return new List<Identifiable.Id>();
            var l = new List<Identifiable.Id>();
            def.Diet.RefreshEatMap(GameContext.Instance.SlimeDefinitions, def);
            if (def.Diet.EatMap != null)
                foreach (var e in def.Diet.EatMap)
                    if (e.isFavorite && e.eats != Identifiable.Id.NONE && e.becomesId != Identifiable.Id.TARR_SLIME && (Config.IncludeLargosInEatInfo || !(GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable.TryGetValue(e.eats, out var d) && d && (d.IsLargo || Identifiable.IsLargo(d.IdentifiableId))) || !(e.becomesId != Identifiable.Id.NONE && GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable.TryGetValue(e.becomesId, out d) && d && (d.IsLargo || Identifiable.IsLargo(d.IdentifiableId)))))
                        l.AddUnique(e.eats);
            return l;
        }
        public static List<Identifiable.Id> GetAllProduces(GordoRewardsBase rewards)
        {
            var l = new List<Identifiable.Id>();
            if (!rewards)
                return l;
            var c = 0;
            foreach (var r in rewards.activeRewards)
            {
                c++;
                if (c > GordoRewardsBase.spawns.Length)
                    break;
                if (!r)
                    continue;
                var i = Identifiable.GetId(r);
                if (i != Identifiable.Id.NONE)
                    l.AddUnique(i);
            }
            if (c < GordoRewardsBase.spawns.Length && rewards.slimePrefab)
            {
                var i = Identifiable.GetId(rewards.slimePrefab);
                if (i != Identifiable.Id.NONE)
                    l.AddUnique(i);
            }
            return l;
        }
    }

    public class ItemDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public static List<ItemDetector> entered = new List<ItemDetector>();
        public Image[] images;
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!entered.Contains(this))
                entered.Insert(0, this);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            entered.Remove(this);
        }
        void ISelectHandler.OnSelect(BaseEventData eventData) => OnPointerEnter(null);
        void IDeselectHandler.OnDeselect(BaseEventData eventData) => OnPointerExit(null);
        public Identifiable.Id? Id
        {
            get
            {
                if (images != null)
                    foreach (var i in images)
                        if (i?.sprite?.texture && FindId(i.sprite.texture, out var r) && r != Identifiable.Id.NONE)
                            return r;
                return null;
            }
        }
        void OnDestroy() => OnPointerExit(null);
        static bool FindId(Texture2D texture, out Identifiable.Id id) =>
            SceneContext.Instance.PediaDirector.entryDict.TryFind(
                x => x.Value.icon?.texture == texture
                    && SceneContext.Instance.PediaDirector.identDict.TryFind(
                        y => y.Value == x.Key,
                        y => y.Key,
                        out var r)
                    && r != Identifiable.Id.NONE,
                x => SceneContext.Instance.PediaDirector.identDict.First(y => y.Value == x.Key).Key,
                out id)
            || GameContext.Instance.LookupDirector.vacItemDict.TryFind(
                x => x.Value.Icon?.texture == texture,
                x => x.Key,
                out id)
            || GameContext.Instance.SlimeDefinitions.slimeDefinitionsByIdentifiable.TryFind(
                x => x.Value.Appearances.Any(y => y.Icon?.texture == texture),
                x => x.Key,
                out id)
            || FallbackFindId(texture, out id);
        static bool FallbackFindId(Texture2D texture, out Identifiable.Id id)
        {
            foreach (var f in Main.AdditionalImageMatchers)
            {
                var r = f(texture);
                if (r == null || r.Value == Identifiable.Id.NONE)
                    continue;
                id = r.Value;
                return true;
            }
            id = Identifiable.Id.NONE;
            return false;
        }
    }

    class ExecuteOnStart : MonoBehaviour
    {
        public Action start;
        void Start()
        {
            start?.Invoke();
            DestroyImmediate(this);
        }
    }

    [ConfigFile("settings")]
    public static class Config
    {
        public static readonly bool IncludeLargosInEatInfo = false;
    }

    [HarmonyPatch(typeof(BaseUI), "Awake")]
    static class Patch_BaseUI_Awake
    {
        static void Postfix(BaseUI __instance) {
            //Main.activeUIs.Insert(0, __instance);
            //__instance.onDestroy += () => Main.activeUIs.Remove(__instance);
            __instance.GetOrAddComponent<ExecuteOnStart>().start = delegate
            {
                var s = __instance.GetComponentsInChildren<Selectable>(true);
                foreach (var o in s)
                {
                    var c = o.GetComponentsInChildren<Image>(true);
                    o.GetOrAddComponent<ItemDetector>().images = c;
                }
            };
        }

        public static void AddDetectors(BaseUI instance)
        {
            var s = instance.GetComponentsInChildren<Selectable>(true);
            foreach (var o in s)
            {
                var c = o.GetComponentsInChildren<Image>(true);
                o.GetOrAddComponent<ItemDetector>().images = c;
            }
        }
    }

    [HarmonyPatch(typeof(PediaUI), "BuildListing")]
    static class Patch_PediaUI_BuildListing
    {
        static void Postfix(PediaUI __instance) => Patch_BaseUI_Awake.AddDetectors(__instance);
    }

    [HarmonyPatch(typeof(AmmoSlotUI), "Awake")]
    static class Patch_AmmoSlotUI_Awake
    {
        static void Postfix(AmmoSlotUI __instance)
        {
            Patch_StorageSlotUI_Awake.TryAddRaycaster(__instance);
            foreach (var t in __instance.slots)
            {
                //t.icon.GetOrAddComponent<ItemDetector>().images = new[] { t.icon };
                t.back.GetOrAddComponent<ItemDetector>().images = new[] { t.icon };
                //t.icon.raycastTarget = true;
            }
        }
    }

    [HarmonyPatch(typeof(StorageSlotUI), "Awake")]
    static class Patch_StorageSlotUI_Awake
    {
        public static List<GraphicRaycaster> slotRaycasters = new List<GraphicRaycaster>();
        static void Postfix(StorageSlotUI __instance)
        {
            var i = __instance.GetComponent<Image>();
            if (!i)
            {
                i = __instance.gameObject.AddComponent<Image>();
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, new Color(0, 0, 0, 0.1f));
                tex.Apply();
                i.sprite = tex.CreateSprite();
            }
            TryAddRaycaster(__instance);
            __instance.GetOrAddComponent<ItemDetector>().images = new[] { __instance.slotIcon };
        }

        public static void TryAddRaycaster(Component component)
        {
            if (!component?.GetComponentInParent<GraphicRaycaster>())
            {
                var g = component?.GetComponentInParent<Canvas>()?.gameObject;
                if (g)
                    slotRaycasters.Add(g.AddComponent<GraphicRaycaster>());
            }
        }
        public static void TryAddRaycaster(GameObject component)
        {
            if (!component?.GetComponentInParent<GraphicRaycaster>())
            {
                var g = component?.GetComponentInParent<Canvas>()?.gameObject;
                if (g)
                    slotRaycasters.Add(g.AddComponent<GraphicRaycaster>());
            }
        }
    }

    [HarmonyPatch(typeof(Image), "OnEnable")]
    static class Patch_Image_OnEnable
    {
        static void Postfix(Image __instance)
        {
            if (__instance.GetComponent<ItemDetector>())
                return;
            Patch_StorageSlotUI_Awake.TryAddRaycaster(__instance);
            __instance.gameObject.AddComponent<ItemDetector>().images = new[] { __instance };
        }
    }

    [HarmonyPatch(typeof(MarketUI), "Awake")]
    static class Patch_MarketUI_Awake
    {
        static void Postfix(MarketUI __instance)
        {
            Patch_StorageSlotUI_Awake.TryAddRaycaster(__instance);
        }
    }

    /*[HarmonyPatch(typeof(ExchangeProgressItemEntryUI), "Awake")]
    static class Patch_ExchangeProgressItemEntryUI_Awake
    {
        static void Postfix(ExchangeProgressItemEntryUI __instance)
        {
            Patch_StorageSlotUI_Awake.TryAddRaycaster(__instance);
            __instance.icon.GetOrAddComponent<ItemDetector>().images = new[] { __instance.icon };
        }
    }

    [HarmonyPatch(typeof(ExchangeRewardItemEntryUI), "Awake")]
    static class Patch_ExchangeRewardItemEntryUI_Awake
    {
        static void Postfix(ExchangeRewardItemEntryUI __instance)
        {
            Patch_StorageSlotUI_Awake.TryAddRaycaster(__instance);
            __instance.icon.GetOrAddComponent<ItemDetector>().images = new[] { __instance.icon };
        }
    }*/
}