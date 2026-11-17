//! A blueprint of mine renamed (the owner's request of 2026-11-17): its name and description, in place in its tab or in
//! the Library's row, its id and file kept so that nothing naming it moves, characters and `.d3b` files included.

use super::Refusal;
use crate::bot::blueprint::Blueprint;

/// Names my blueprint `name`, without the spaces around it, and describes it `description`; a blank name is refused.
pub(super) fn rename(blueprint: &mut Blueprint, name: &str, description: &str) -> Result<(), Refusal> {
    let name = name.trim();
    if name.is_empty() {
        return Err(Refusal::Blank);
    }
    name.clone_into(&mut blueprint.name);
    description.trim().clone_into(&mut blueprint.description);
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::super::{Edit, edit};
    use super::*;
    use crate::bot::blueprint::Id;
    use crate::bot::json::{read, tests::HARVEST};

    fn harvest() -> Blueprint {
        read(HARVEST).unwrap_or_else(|error| panic!("{error}"))
    }

    fn about(name: &str, description: &str) -> Edit {
        Edit::About {
            name: name.into(),
            description: description.into(),
        }
    }

    #[test]
    fn a_blueprint_renamed_keeps_its_id_and_all_it_holds() {
        let mut blueprint = harvest();
        let before = blueprint.clone();
        let renamed = edit(
            &mut blueprint,
            &Id("events".into()),
            about(" Mes blés ", "Le blé d'Astrub.\n"),
        );
        assert_eq!(renamed, Ok(()));
        assert_eq!(
            (blueprint.name.as_str(), blueprint.description.as_str()),
            ("Mes blés", "Le blé d'Astrub.")
        );
        let kept = Blueprint {
            name: before.name.clone(),
            description: before.description.clone(),
            ..blueprint.clone()
        };
        assert_eq!(kept, before, "its id, version, variables and graphs as they were");
        assert_eq!(
            edit(&mut blueprint, &Id("events".into()), about("Mes blés", "")),
            Ok(())
        );
        assert!(blueprint.description.is_empty(), "a description may be emptied");
    }

    #[test]
    fn a_blank_name_is_refused_and_changes_nothing() {
        let mut blueprint = harvest();
        let before = blueprint.clone();
        assert_eq!(
            edit(&mut blueprint, &Id("events".into()), about("  ", "Ailleurs")),
            Err(Refusal::Blank)
        );
        assert_eq!(blueprint, before);
    }
}
