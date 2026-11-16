//! A run's variables (`docs/bot.md`, "The concepts"): each from its memory when it keeps one, else from the
//! character's setting, else its default; memory's changes are handed back, to be saved in the character's file at
//! safe points (the owner, 2026-11-16).

use std::collections::{BTreeMap, BTreeSet};

use serde_json::Value;

use super::behaviour::Fault;
use super::program::Refused;
use crate::bot::variable::Variable;

/// The values of a run's variables.
#[derive(Debug)]
pub(super) struct Variables {
    values: BTreeMap<String, Value>,
    /// What each variable accepts.
    kinds: BTreeMap<String, Variable>,
    memory: BTreeSet<String>,
    changed: bool,
}

impl Variables {
    /// The variables of a blueprint for a character of these `settings` and this `memory`: a value the variable does
    /// not accept is left out, and one left with no value refuses the run.
    pub(super) fn new(
        variables: &[Variable],
        settings: &BTreeMap<String, Value>,
        memory: &BTreeMap<String, Value>,
    ) -> Result<Self, Refused> {
        let mut values = BTreeMap::new();
        for variable in variables {
            let value = own(variable, memory, variable.memory)
                .or_else(|| own(variable, settings, variable.per_character))
                .or(Some(&variable.default).filter(|value| !value.is_null()))
                .ok_or_else(|| Refused::Unset {
                    variable: variable.id.0.clone(),
                })?;
            values.insert(variable.id.0.clone(), value.clone());
        }
        let memory = variables
            .iter()
            .filter(|variable| variable.memory)
            .map(|variable| variable.id.0.clone())
            .collect();
        let kinds = variables
            .iter()
            .map(|variable| (variable.id.0.clone(), variable.clone()))
            .collect();
        Ok(Self {
            values,
            kinds,
            memory,
            changed: false,
        })
    }

    pub(super) fn get(&self, id: &str) -> Option<&Value> {
        self.values.get(id)
    }

    /// Sets `id`, refused when out of its bounds: memory would lose it at the next run.
    pub(super) fn set(&mut self, id: &str, value: Value) -> Result<(), Fault> {
        if self.kinds.get(id).is_some_and(|variable| !variable.accepts(&value)) {
            return Err(Fault::Refused {
                variable: id.to_owned(),
            });
        }
        self.changed |= self.memory.contains(id) && self.values.get(id) != Some(&value);
        self.values.insert(id.to_owned(), value);
        Ok(())
    }

    /// The memory variables' values.
    pub(super) fn memory(&self) -> BTreeMap<String, Value> {
        self.memory
            .iter()
            .filter_map(|id| Some((id.clone(), self.values.get(id)?.clone())))
            .collect()
    }

    /// Whether memory changed since this was last asked.
    pub(super) fn take_changed(&mut self) -> bool {
        std::mem::take(&mut self.changed)
    }
}

/// `variable`'s own value in `from`, when it keeps one there (`kept`) and accepts it.
fn own<'a>(variable: &Variable, from: &'a BTreeMap<String, Value>, kept: bool) -> Option<&'a Value> {
    from.get(&variable.id.0).filter(|value| kept && variable.accepts(value))
}
