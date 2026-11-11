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

#[cfg(test)]
mod tests {
    use super::*;

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
