//! Duplicate (`docs/bot.md`, "The concepts"): a blueprint of mine made from another, fully editable, noting what it is
//! based on. Its texts come from the interface, in the player's language, since a given blueprint's are translations
//! (`bot::given`); its id from its name, unique among the blueprints there are.

use std::collections::{BTreeMap, HashSet};

use super::blueprint::{BasedOn, Blueprint, Id};

/// A copy's texts, as the player reads them: its name, its description, and each variable's name and help by id. A
/// variable the map leaves out keeps the source's.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub(crate) struct Texts {
    pub(crate) name: String,
    pub(crate) description: String,
    pub(crate) variables: BTreeMap<Id, (String, String)>,
}

/// The copy of `source`, under `id`, with `texts`. A copy of a given blueprint is based on it; a copy of mine is based on
/// what mine was based on, so that it hears of the same original's new versions.
pub(crate) fn copy(source: &Blueprint, given: bool, id: Id, texts: Texts) -> Blueprint {
    let mut copy = source.clone();
    copy.id = id;
    copy.name = texts.name;
    copy.description = texts.description;
    copy.based_on = if given {
        Some(BasedOn {
            id: source.id.clone(),
            version: source.version,
        })
    } else {
        source.based_on.clone()
    };
    for variable in &mut copy.variables {
        if let Some((name, help)) = texts.variables.get(&variable.id) {
            variable.name.clone_from(name);
            variable.help.clone_from(help);
        }
    }
    copy
}

/// An id made from `name`: lowercase letters and digits, accents dropped, anything else a dash, and a number after it
/// when `taken` holds it already.
pub(crate) fn id_for(name: &str, taken: &HashSet<Id>) -> Id {
    let mut slug = String::new();
    for character in name.to_lowercase().chars() {
        match unaccented(character) {
            Some(letter) => slug.push(letter),
            None if !slug.ends_with('-') && !slug.is_empty() => slug.push('-'),
            None => {}
        }
    }
    // Room for "-<n>" within an id's limit.
    let mut slug: String = slug.trim_end_matches('-').chars().take(Id::LIMIT - 8).collect();
    slug.truncate(slug.trim_end_matches('-').len());
    if slug.is_empty() {
        slug.push_str("blueprint");
    }
    let mut candidate = Id(slug.clone());
    let mut number = 2;
    while taken.contains(&candidate) || !candidate.is_well_formed() {
        candidate = Id(format!("{slug}-{number}"));
        number += 1;
    }
    candidate
}

fn unaccented(character: char) -> Option<char> {
    let letter = match character {
        'à' | 'â' | 'ä' | 'á' | 'ã' => 'a',
        'ç' => 'c',
        'é' | 'è' | 'ê' | 'ë' => 'e',
        'î' | 'ï' | 'í' => 'i',
        'ô' | 'ö' | 'ó' | 'õ' => 'o',
        'ù' | 'û' | 'ü' | 'ú' => 'u',
        'ñ' => 'n',
        'ÿ' => 'y',
        other => other,
    };
    (letter.is_ascii_lowercase() || letter.is_ascii_digit()).then_some(letter)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::given;

    fn id(text: &str) -> Id {
        Id(text.to_owned())
    }

    fn harvest() -> Blueprint {
        let given = given().unwrap_or_else(|error| panic!("{error}"));
        given.into_iter().next().unwrap_or_else(|| panic!("no given blueprint"))
    }

    #[test]
    fn a_copy_of_a_given_blueprint_is_based_on_it_and_speaks_the_players_language() {
        let source = harvest();
        let texts = Texts {
            name: "Récolte automatique (copie)".to_owned(),
            description: "Récolte ce qui pousse.".to_owned(),
            variables: BTreeMap::from([(id("zone"), ("Zone".to_owned(), "Où récolter.".to_owned()))]),
        };
        let copy = copy(&source, true, id("recolte-automatique-copie"), texts);
        assert_eq!(
            copy.based_on,
            Some(BasedOn {
                id: source.id.clone(),
                version: source.version
            })
        );
        assert_eq!(
            (copy.name.as_str(), copy.variables[0].help.as_str()),
            ("Récolte automatique (copie)", "Où récolter.")
        );
        assert_eq!(
            copy.variables[1].name, source.variables[1].name,
            "a variable left out keeps the source's"
        );
        assert_eq!(copy.graphs, source.graphs);
    }

    #[test]
    fn a_copy_of_mine_is_based_on_what_mine_was_based_on() {
        let original = harvest();
        let mine = copy(&original, true, id("mine"), Texts::default());
        let again = copy(&mine, false, id("again"), Texts::default());
        assert_eq!(again.based_on, mine.based_on);
        let unbased = copy(
            &Blueprint { based_on: None, ..mine },
            false,
            id("third"),
            Texts::default(),
        );
        assert_eq!(unbased.based_on, None);
    }

    #[test]
    fn an_id_is_made_from_the_name_without_accents() {
        let none = HashSet::new();
        assert_eq!(
            id_for("Récolte automatique (copie)", &none),
            id("recolte-automatique-copie")
        );
        assert_eq!(id_for("  Ma chasse : 2  ", &none), id("ma-chasse-2"));
    }

    #[test]
    fn an_id_taken_gets_a_number_and_a_name_with_no_letter_gets_one() {
        let taken = HashSet::from([id("chasse-copie"), id("chasse-copie-2")]);
        assert_eq!(id_for("Chasse (copie)", &taken), id("chasse-copie-3"));
        assert_eq!(id_for("★ ★", &HashSet::new()), id("blueprint"));
        // A name Windows keeps for a device is no id: it gets a number.
        assert_eq!(id_for("Con", &HashSet::new()), id("con-2"));
        let long = id_for(&"a".repeat(200), &HashSet::new());
        assert!(long.is_well_formed() && long.0.len() <= Id::LIMIT - 8);
    }
}
