use serde::Deserialize;

use super::text::Text;

/// A world of the client's map and the levels of zoom its tiles cut (`WorldMapsDataRoot`, 3.7.13.17). Its tiles are
/// `tiles/<id>/<percent>/<n>.jpg`, numbered from 1 row by row from the top left, 1,024 pixels a side but for the last
/// column and row.
#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct WorldMap {
    pub id: i32,
    pub name: Option<Text>,
    /// Where map 0, 0 begins on the image at zoom 100, in pixels.
    pub origin_x: i32,
    pub origin_y: i32,
    /// A map's size on the image at zoom 100, in pixels.
    pub map_width: f32,
    pub map_height: f32,
    pub min_scale: f32,
    pub max_scale: f32,
    pub start_scale: f32,
    /// The image's size at zoom 100, as the client's table gives it.
    pub width: i32,
    pub height: i32,
    pub visible: bool,
    /// The levels of zoom, largest first.
    pub zooms: Vec<Zoom>,
}

/// One level of zoom of a world's image: the size its tiles add up to.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Zoom {
    pub percent: u32,
    pub width: u32,
    pub height: u32,
    pub columns: u32,
    pub rows: u32,
}

/// A hint of the world map (`HintsData`, 3.7.13.17, `main:docs/client.md`, "Hints"): what stands on a map, a zaap,
/// a bank or a dungeon's door, drawn with its picture `icons/hints/<gfx>.png`, which the client lacks for a few.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Hint {
    pub id: i32,
    pub category_id: i32,
    /// Its picture's number, which tells apart what a category mixes: 401 a bank, 410 a zaap.
    pub gfx: i32,
    pub name: Text,
    pub map_id: i32,
    pub x: i32,
    pub y: i32,
    pub outdoor: bool,
    pub sub_area_id: i32,
    /// The world the client draws it on, which for 36 hints is not its map's.
    pub world_map_id: i32,
    /// A dungeon's level, 0 for most others.
    pub level: u32,
}

/// What a hint is, for the switches of the World's map: the client's categories mix what a bot tells apart, the zaaps
/// among the boats in Transports and the banks among the taverns in Divers, so the picture is read before the category
/// (`main:docs/client.md`, "Hints"). In order of use to a bot, which keeps the more useful of two crowded pictures (the
/// owner, 2026-11-15): the zaaps it travels by, the banks it ends at, dungeons, workshops, auction houses, class
/// places, the other transports, the other places, and last the passages between areas, which only a close view needs.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub enum HintKind {
    Zaap,
    Bank,
    Dungeon,
    Workshop,
    AuctionHouse,
    ClassPlace,
    Transport,
    Place,
    Passage,
}

impl Hint {
    /// What the hint is; a category that holds none on 3.7.13.17 (conquest territories, quests…) is a place.
    #[must_use]
    pub fn kind(&self) -> HintKind {
        match (self.gfx, self.category_id) {
            (410, _) => HintKind::Zaap,
            (401, _) => HintKind::Bank,
            (900..=903, _) => HintKind::Passage,
            (_, 1) => HintKind::ClassPlace,
            (_, 2) => HintKind::AuctionHouse,
            (_, 3) => HintKind::Workshop,
            (_, 6) => HintKind::Dungeon,
            (_, 9) => HintKind::Transport,
            _ => HintKind::Place,
        }
    }
}

/// A category of the hints (`HintCategoryData`, 3.7.13.17), such as Transports; 6 of the 12 hold hints.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct HintCategory {
    pub id: i32,
    pub name: Text,
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write these files byte for byte (tools/extractor/tests/Hints/HintTablesTests.cs).
    const HINTS: &str = include_str!("../../../tools/extractor/tests/fixtures/hints/hints.json");
    const CATEGORIES: &str = include_str!("../../../tools/extractor/tests/fixtures/hints/hint-categories.json");

    #[test]
    fn reads_the_hints_and_categories_the_extractor_writes() {
        let hints: Vec<Hint> = serde_json::from_str(HINTS).unwrap_or_else(|error| panic!("{error}"));
        let categories: Vec<HintCategory> = serde_json::from_str(CATEGORIES).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            (
                hints[0].gfx,
                hints[0].name.fr.as_str(),
                hints[0].outdoor,
                hints[0].world_map_id
            ),
            (401, "Banque", true, 1)
        );
        assert_eq!((hints[1].level, hints[1].outdoor), (50, false));
        assert_eq!((categories[0].id, categories[0].name.en.as_str()), (4, "Miscellaneous"));
    }

    #[test]
    fn tells_a_hint_s_kind_by_its_picture_before_its_category() {
        let hints: Vec<Hint> = serde_json::from_str(HINTS).unwrap_or_else(|error| panic!("{error}"));
        let kind = |gfx: i32, category_id: i32| {
            Hint {
                gfx,
                category_id,
                ..hints[0].clone()
            }
            .kind()
        };
        // A zaap is a transport, a bank and a passage are in Divers, by the client's categories.
        assert_eq!(kind(410, 9), HintKind::Zaap);
        assert_eq!(kind(401, 4), HintKind::Bank);
        assert_eq!(kind(902, 4), HintKind::Passage);
        assert_eq!(kind(413, 9), HintKind::Transport);
        assert_eq!(kind(408, 4), HintKind::Place);
        assert_eq!(
            [(80, 1), (226, 2), (311, 3), (422, 6)].map(|(gfx, category)| kind(gfx, category)),
            [
                HintKind::ClassPlace,
                HintKind::AuctionHouse,
                HintKind::Workshop,
                HintKind::Dungeon
            ]
        );
        // A category that holds no hint yet, such as Quests.
        assert_eq!(kind(500, 11), HintKind::Place);
        assert!(HintKind::Zaap < HintKind::Bank && HintKind::Place < HintKind::Passage);
    }

    #[test]
    fn refuses_a_hint_of_another_shape() {
        let unknown = HINTS.replacen(r#""level":0"#, r#""level":0,"realMapId":0"#, 1);
        assert!(serde_json::from_str::<Vec<Hint>>(&unknown).is_err());
    }

    // The extractor's own tests write this file byte for byte (tools/extractor/tests/WorldMaps/WorldMapTablesTests.cs).
    const WORLDS: &str = include_str!("../../../tools/extractor/tests/fixtures/world/worlds.json");

    #[test]
    fn reads_the_worlds_the_extractor_writes() {
        let worlds: Vec<WorldMap> = serde_json::from_str(WORLDS).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            worlds[0].zooms[1],
            Zoom {
                percent: 20,
                width: 2000,
                height: 1932,
                columns: 2,
                rows: 2
            }
        );
        assert!((worlds[0].map_width - 69.5).abs() < f32::EPSILON);
    }
}
