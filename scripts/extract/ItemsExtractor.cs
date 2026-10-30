using D3B.Data.Effects;
using D3B.Data.Items;
using D3B.Data.Spells;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the items and their types, and the weapons with their effects and their types' zones.</summary>
/// <remarks>
/// <para>
/// An item keeps what a bot names, sorts and weighs it by, and its icon, which the pack holds for some items only. Its effects, recipes, criteria and <c>m_flags</c>, whose bits
/// are not measured, are left out (client.md).
/// </para>
/// <para>
/// A weapon keeps its fight fields and its effects, which its record names by reference, and its type the zone the client
/// writes for it. The fields of a weapon's effect that hold the same on every one of client 3.7.12.12 are left out: the
/// extraction fails when one no longer does, or when a weapon is of a type that is no weapon's or has no zone, rather than
/// drop what a later client means by it. A few effects name one the client lacks, which no fight uses: they are counted.
/// </para>
/// </remarks>
internal static class ItemsExtractor
{
    // The client's superTypeId of every weapon's type in client 3.7.12.12, and of no other type.
    private const int WeaponSuperType = 2;

    // The zone the client writes on every weapon's effect of client 3.7.12.12, a circle of size 1: its type's zone is the one a blow hits.
    private const byte EffectZoneShape = (byte)'C';

    private static readonly string[] _tables = ["items", "itemtypes"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static (ItemCatalog Items, WeaponCatalog Weapons) Read(ClientInstall client, EffectCatalog effects)
    {
        try
        {
            var (records, references) = UnityDataTable.ReadByIdWithReferences(client, "items");
            var typeRecords = UnityDataTable.ReadById(client, "itemtypes");
            var items = records.Select(record => new Item(
                record.Int32("id"),
                checked((int)record.UInt32("nameId")),
                record.UInt16("typeId"),
                record.Byte("level"),
                record.UInt16("realWeight"),
                record.Int32("iconId")));
            var types = typeRecords.Select(record => new ItemType(
                record.Int32("id"),
                record.Int32("nameId"),
                record.Int32("superTypeId"),
                record.Int32("categoryId")));
            var catalog = new ItemCatalog(items.OrderBy(item => item.Id).ToArray(), types.OrderBy(type => type.Id).ToArray());
            var untyped = 0;

            // A test item of client 3.7.12.12 names a type the client lacks; JobsExtractor fails when a resource does.
            foreach (var item in catalog.Items.Span)
            {
                if (!catalog.TryGetType(item.TypeId, out _))
                    untyped++;
            }

            Console.WriteLine($"Items: {catalog.Items.Length:N0}, {catalog.Types.Length:N0} types; {untyped:N0} of a type the client lacks");

            var weapons = Weapons(records, references, typeRecords, catalog, effects);

            return (catalog, weapons);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException or KeyNotFoundException)
        {
            throw new InvalidDataException($"The items' tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static WeaponCatalog Weapons(IReadOnlyList<UnityFields> records, IReadOnlyDictionary<long, UnityFields> references, IReadOnlyList<UnityFields> typeRecords, ItemCatalog items, EffectCatalog effects)
    {
        var zones = new Dictionary<string, int>(StringComparer.Ordinal);
        var weaponTypes = new List<WeaponType>();

        foreach (var record in typeRecords.Where(record => record.Int32("superTypeId") is WeaponSuperType).OrderBy(record => record.Int32("id")))
        {
            var id = record.Int32("id");
            var zone = record.String("rawZone");

            if (zone.Length is 0)
                throw new InvalidDataException($"Weapon type {id} has no zone, which every weapon's type of client 3.7.12.12 has.");

            if (!zones.TryGetValue(zone, out var index))
            {
                index = zones.Count;
                zones.Add(zone, index);
            }

            weaponTypes.Add(new WeaponType { Id = id, Zone = index });
        }

        var weapons = new List<Weapon>();
        var weaponEffects = new List<WeaponEffect>();
        var inFights = 0;
        var unknown = new Dictionary<int, int>();

        // Only a weapon's record holds the fight fields: the client's WeaponData, the others ItemData.
        foreach (var record in records.Where(record => record.Has("apCost")).OrderBy(record => record.Int32("id")))
        {
            var id = record.Int32("id");
            var typeId = record.UInt16("typeId");

            if (!items.TryGetType(typeId, out var type) || type.SuperTypeId is not WeaponSuperType)
                throw new InvalidDataException($"Weapon {id} is of type {typeId}, which is no weapon's type.");

            var properties = (record.Boolean("castInLine") ? SpellLevelProperties.CastInLine : SpellLevelProperties.None)
                | (record.Boolean("castInDiagonal") ? SpellLevelProperties.CastInDiagonal : SpellLevelProperties.None)
                | (record.Boolean("castTestLos") ? SpellLevelProperties.CastTestLos : SpellLevelProperties.None);
            var first = weaponEffects.Count;

            foreach (var item in record.Items("possibleEffects"))
            {
                var effect = Effect(id, references[item.Reference("rid")]);

                // 205 and others name an effect the client lacks, which no fight can use: kept as the client holds them, and counted.
                if (!effects.TryGetEffect(effect.EffectId, out var found))
                    unknown[effect.EffectId] = unknown.GetValueOrDefault(effect.EffectId) + 1;
                else if ((found.Properties & EffectProperties.UseInFight) is not 0)
                    inFights++;

                weaponEffects.Add(effect);
            }

            weapons.Add(new Weapon
            {
                ItemId = id,
                TypeId = typeId,
                ApCost = checked((short)record.Int32("apCost")),
                MinRange = checked((byte)record.Int32("minRange")),
                Range = checked((byte)record.Int32("range")),
                CriticalHitProbability = checked((short)record.Int32("criticalHitProbability")),
                CriticalHitBonus = checked((short)record.Int32("criticalHitBonus")),
                MaxCastPerTurn = checked((sbyte)record.Int32("maxCastPerTurn")),
                Properties = properties,
                FirstEffect = first,
                EffectCount = checked((byte)(weaponEffects.Count - first)),
            });
        }

        var texts = new string[zones.Count];

        foreach (var (text, index) in zones)
            texts[index] = text;

        var catalog = new WeaponCatalog(weapons.ToArray(), weaponEffects.ToArray(), weaponTypes.ToArray(), texts);

        Console.WriteLine($"Weapons: {catalog.Weapons.Length:N0} of {catalog.Types.Length:N0} types, {catalog.Zones.Length:N0} zones; {catalog.Effects.Length:N0} effects, {inFights:N0} of them used in fights, "
            + $"{unknown.Values.Sum():N0} of {unknown.Count:N0} effects the client lacks ({string.Join(", ", unknown.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key} {pair.Value}"))})");

        return catalog;
    }

    private static WeaponEffect Effect(int weaponId, UnityFields record)
    {
        var effectId = record.UInt16("actionId");
        var zone = record.Nested("zoneDescr");

        // Constant on every weapon's effect of client 3.7.12.12, so left out of the pack: a client setting one means something new.
        if (record.Int32("spellId") is not -1 || record.Int32("effectUid") is not 0 || record.Int32("order") is not 0 || record.Int16("targetId") is not 0
            || record.Int16("modificator") is not 0 || record.Boolean("displayZero") || record.String("targetMask").Length is not 0 || record.String("triggers").Length is not 0
            || record.SByte("duration") is not 0 || record.Single("random") is not 0 || record.Int16("group") is not 0 || record.Byte("delay") is not 0
            || record.Byte("dispellable") is not 1 || record.SByte("effectElement") is not -1 || record.Int32("effectTriggerDuration") is not 0
            || record.Byte("m_flags") is not 15 || zone.Byte("shape") is not EffectZoneShape || zone.Byte("param1") is not 1 || zone.Byte("param2") is not 0)
        {
            throw new InvalidDataException($"An effect of weapon {weaponId} sets a field the extractor measured the same on every weapon's effect: its spell, uid, order, target, modifier, mask, triggers, duration, draw, delay, dispel, element, flags or zone.");
        }

        return new WeaponEffect
        {
            EffectId = effectId,
            DiceNum = record.Int32("diceNum"),
            DiceSide = record.Int32("diceSide"),
            Value = record.Int32("value"),
        };
    }
}
