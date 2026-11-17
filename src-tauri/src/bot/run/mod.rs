//! The runtime (`docs/bot.md`, "The runtime", ADR 0017): a blueprint run for one character, a node at a time, against
//! the observed state and on a clock it is given. Each kind's behaviour is its own; the engine follows the flow, calls
//! functions, pulls values and stops on what blocks. Step 4's Play drives it; nothing acts in the game before.

mod behaviour;
mod frame;
mod kinds;
mod knows;
mod program;
mod values;
mod variables;

#[cfg(test)]
mod replay;
#[cfg(test)]
mod tests;

use std::collections::{BTreeMap, HashMap};
use std::time::Instant;

use serde_json::Value;

pub(crate) use behaviour::{Behaviour, Cx, Exit, Fault, Notice, Resume, Step};
pub(crate) use knows::Knows;
pub(crate) use program::Refused;
pub(crate) use values::Trace;

use super::Blueprint;
use super::catalogue::{DONE, FAILED, REASON};
use crate::game::View;
use frame::{Body, Call, Chain, Slot};
use program::{Doing, Program, compile};
use variables::Variables;

/// How many nodes may run in a row without the flow waiting: more "runs without pause" (the owner, 2026-11-16).
pub(crate) const WITHOUT_PAUSE: usize = 10_000;
/// How many calls may be at work, one inside the other (settled alone): more is a function calling itself without end.
pub(crate) const DEEPEST: usize = 64;

/// Where a run is.
#[derive(Debug, Clone, PartialEq)]
pub(crate) enum State {
    Ready,
    Running,
    Paused,
    Finished,
    Blocked(Blocked),
}

/// Why and where a run is blocked.
#[derive(Debug, Clone, PartialEq)]
pub(crate) struct Blocked {
    pub(crate) reason: Reason,
    pub(crate) graph: String,
    pub(crate) node: String,
}

/// Why a run is blocked, each worded by the interface.
#[derive(Debug, Clone, PartialEq)]
pub(crate) enum Reason {
    /// An action or a function failed with this reason, and nothing was wired to its Failed.
    Failed(String),
    /// A Block node's reason.
    Said(String),
    /// [`WITHOUT_PAUSE`] nodes in a row without waiting.
    WithoutPause,
    /// Calls deeper than [`DEEPEST`].
    TooDeep,
    /// A value that cannot be had.
    Fault(Fault),
    /// The character died: the game forces it, and the blueprint does not handle it before (26).
    Died,
}

/// A blueprint run for one character.
pub(crate) struct Run {
    program: Program,
    /// The flows at work, the one running last: (26)'s events push their own; one, the main flow, today.
    flows: Vec<Vec<Call>>,
    variables: Variables,
    trace: Trace,
    notices: Vec<Notice>,
    /// The fights the character had lost when the run first saw it: one more is a death.
    lost: Option<u32>,
    state: State,
    /// When a node waiting on the clock wants to be advanced again.
    wake: Option<Instant>,
    /// Whether the node at work must start over at the next advance: the character was away, or the run paused.
    again: bool,
    /// What the nodes that remember keep for the whole run, by graph and node: Do N times', Alternate's, Pick's.
    kept: HashMap<(usize, usize), Slot>,
}

/// What the run does after a step.
enum Then {
    Runs,
    Stops,
}

/// What a node at work asked for.
enum Next {
    Step(Step),
    Call {
        graph: usize,
        inputs: BTreeMap<String, Value>,
    },
}

impl Run {
    /// `blueprint` ready to run with a character's `settings` and `memory`, refused when it has a problem, a kind that
    /// cannot run yet, no On start, or a variable left without value.
    pub(crate) fn new(
        blueprint: &Blueprint,
        settings: &BTreeMap<String, Value>,
        memory: &BTreeMap<String, Value>,
    ) -> Result<Self, Refused> {
        let program = compile(blueprint)?;
        let variables = Variables::new(&blueprint.variables, settings, memory)?;
        Ok(Self::of(program, variables))
    }

    fn of(program: Program, variables: Variables) -> Self {
        let (graph, node) = program.main;
        Self {
            program,
            flows: vec![vec![Call::new(graph, None, node)]],
            variables,
            trace: Trace::default(),
            notices: Vec::new(),
            lost: None,
            state: State::Ready,
            wake: None,
            again: false,
            kept: HashMap::new(),
        }
    }

    pub(crate) fn state(&self) -> &State {
        &self.state
    }

    /// When the node at work waits on the clock, the latest moment to advance it again.
    pub(crate) fn wake(&self) -> Option<Instant> {
        self.wake
    }

    pub(crate) fn trace(&self) -> &Trace {
        &self.trace
    }

    /// What Notify told since this was last asked, oldest first.
    pub(crate) fn notices(&mut self) -> Vec<Notice> {
        std::mem::take(&mut self.notices)
    }

    /// The memory variables' values, and whether they changed since this was last asked.
    pub(crate) fn memory(&mut self) -> (BTreeMap<String, Value>, bool) {
        let changed = self.variables.take_changed();
        (self.variables.memory(), changed)
    }

    /// Pauses a ready or running run; the node at work starts over when it resumes.
    pub(crate) fn pause(&mut self) {
        if matches!(self.state, State::Ready | State::Running) {
            self.state = State::Paused;
            self.again = true;
            self.wake = None;
        }
    }

    pub(crate) fn resume(&mut self) {
        if self.state == State::Paused {
            self.state = State::Running;
        }
    }

    /// Runs nodes at `now` until the flow waits, ends or blocks, with what `knows` answers. Without a `view`, the
    /// character is away: nothing runs, and the node at work starts over once it is back. A fight lost since the run
    /// first saw the character (or last saw it, after a count started again) is its death, which blocks it.
    pub(crate) fn advance(&mut self, view: Option<&View>, knows: &dyn Knows, now: Instant) -> &State {
        match self.state {
            State::Ready => self.state = State::Running,
            State::Running => {}
            State::Paused | State::Finished | State::Blocked(_) => return &self.state,
        }
        let Some(view) = view else {
            // The node at work starts over once the character is back: it waits on nothing meanwhile.
            self.again = true;
            self.wake = None;
            return &self.state;
        };
        // The view counts since the character was chosen: a new connection or selection starts it again from 0.
        let lost = self.lost.get_or_insert(view.fights.lost);
        if view.fights.lost > *lost {
            self.block(Reason::Died);
            return &self.state;
        }
        *lost = view.fights.lost;
        if std::mem::take(&mut self.again)
            && let Some(chain) = self.chain()
        {
            chain.resume = Resume::Enter {
                pin: chain.entered.clone(),
            };
        }
        self.wake = None;
        for _ in 0..WITHOUT_PAUSE {
            let then = match self.next(view, knows, now) {
                Ok(Next::Step(step)) => self.apply(step),
                Ok(Next::Call { graph, inputs }) => self.call(graph, inputs),
                Err((fault, failed_at)) => self.block_at(Reason::Fault(fault), failed_at),
            };
            if matches!(then, Then::Stops) {
                return &self.state;
            }
        }
        self.block(Reason::WithoutPause);
        &self.state
    }

    /// The chain at work.
    fn chain(&mut self) -> Option<&mut Chain> {
        self.flows.last_mut()?.last_mut()?.chains.last_mut()
    }

    /// The graph and node at work.
    fn at(&self) -> (usize, usize) {
        let call = self.flows.last().and_then(|flow| flow.last());
        call.and_then(|call| Some((call.graph, call.chains.last()?.at)))
            .unwrap_or(self.program.main)
    }

    /// What the node at work does now; a fault comes with the node without flow it came from, if one.
    fn next(&mut self, view: &View, knows: &dyn Knows, now: Instant) -> Result<Next, (Fault, Option<usize>)> {
        let (graph, node) = self.at();
        let Some(call) = self.flows.last_mut().and_then(|flow| flow.last_mut()) else {
            return Ok(Next::Step(Step::Finish));
        };
        let resume = call
            .chains
            .last_mut()
            .map_or(Resume::Tick, |chain| std::mem::replace(&mut chain.resume, Resume::Tick));
        let mut cx = Cx {
            program: &self.program,
            graph,
            node,
            depth: 0,
            read: HashMap::new(),
            failed_at: None,
            call,
            variables: &mut self.variables,
            kept: &mut self.kept,
            trace: &mut self.trace,
            notices: &mut self.notices,
            view,
            knows,
            now,
        };
        let next = match &self.program.graphs[graph].nodes[node].doing {
            Doing::Kind(behaviour) => behaviour.run(&mut cx, resume).map(Next::Step),
            Doing::Set(variable) => set(&mut cx, variable).map(Next::Step),
            Doing::Call(function) => {
                let mut inputs = BTreeMap::new();
                let mut read = || {
                    for pin in cx.inputs() {
                        let value = cx.input(&pin)?;
                        inputs.insert(pin, value);
                    }
                    Ok(())
                };
                read().map(|()| Next::Call {
                    graph: *function,
                    inputs,
                })
            }
            Doing::Get(_) => Err(Fault::NoFlow),
        };
        let failed_at = cx.failed_at;
        next.map_err(|fault| (fault, failed_at))
    }

    fn apply(&mut self, step: Step) -> Then {
        match step {
            Step::Go(pin) => self.go(&pin),
            Step::Failed(reason) => self.failed(reason),
            Step::Chain { pin, body } => {
                let (graph, node) = self.at();
                let next = self
                    .program
                    .next(graph, node, &pin)
                    .map(|(to, input)| (to, input.to_owned()));
                let Some(call) = self.flows.last_mut().and_then(|flow| flow.last_mut()) else {
                    return Then::Stops;
                };
                match next {
                    Some((to, input)) => call.chains.push(Chain::new(Some(node), body, to, input)),
                    // A body wired to nothing ends at once.
                    None => {
                        if let Some(chain) = call.chains.last_mut() {
                            chain.resume = Resume::ChainEnded;
                        }
                    }
                }
                Then::Runs
            }
            Step::Wait { until } => {
                self.wake = until;
                Then::Stops
            }
            Step::Return { exit, outputs } => self.returned(exit, outputs),
            Step::Leave => self.leave(),
            Step::Pause => {
                self.state = State::Paused;
                self.wake = None;
                Then::Stops
            }
            Step::Finish => self.finish(),
            Step::Block(reason) => self.block(Reason::Said(reason)),
        }
    }

    /// The flow goes out of `pin` of the node at work; wired to nothing, its chain ends.
    fn go(&mut self, pin: &str) -> Then {
        let (graph, node) = self.at();
        match self
            .program
            .next(graph, node, pin)
            .map(|(to, input)| (to, input.to_owned()))
        {
            Some((to, input)) => {
                if let Some(chain) = self.chain() {
                    *chain = Chain::new(chain.owner, chain.body, to, input);
                }
                Then::Runs
            }
            None => self.ended(),
        }
    }

    /// The node at work failed: out of its Failed, or blocked when nothing is wired there.
    fn failed(&mut self, reason: String) -> Then {
        let (graph, node) = self.at();
        if self.program.next(graph, node, FAILED.id).is_none() {
            return self.tried(reason);
        }
        if let Some(call) = self.flows.last_mut().and_then(|flow| flow.last_mut()) {
            call.outputs.insert((node, REASON.id.to_owned()), Value::String(reason));
        }
        self.go(FAILED.id)
    }

    /// The chain at work ended: its owner resumes; a function's own returns Done; the main flow's finishes the run.
    fn ended(&mut self) -> Then {
        let Some(call) = self.flows.last_mut().and_then(|flow| flow.last_mut()) else {
            return Then::Stops;
        };
        let owned = call.chains.last().is_some_and(|chain| chain.owner.is_some());
        if owned {
            call.chains.pop();
            if let Some(chain) = call.chains.last_mut() {
                chain.resume = Resume::ChainEnded;
            }
            return Then::Runs;
        }
        if call.caller.is_some() {
            return self.returned(Exit::Done, BTreeMap::new());
        }
        self.finish()
    }

    /// The node at work calls `graph` with `inputs`, its entry's outputs.
    fn call(&mut self, graph: usize, inputs: BTreeMap<String, Value>) -> Then {
        let (_, node) = self.at();
        let Some(flow) = self.flows.last_mut() else {
            return Then::Stops;
        };
        // The event graph's frame is not a call.
        if flow.len() > DEEPEST {
            return self.block(Reason::TooDeep);
        }
        let Some(entry) = self.program.graphs[graph].entry else {
            // A function without entry does nothing.
            return self.go(DONE.id);
        };
        let mut call = Call::new(graph, Some(node), entry);
        call.outputs = inputs.into_iter().map(|(pin, value)| ((entry, pin), value)).collect();
        // A local without default has none until set: read before, it cannot be had.
        let locals = self.program.graphs[graph].locals.iter();
        call.locals = locals
            .filter(|it| !it.default.is_null())
            .map(|it| (it.id.0.clone(), it.default.clone()))
            .collect();
        flow.push(call);
        Then::Runs
    }

    /// The function at work returned by `exit`: its call goes on out of the matching pin, with its outputs.
    fn returned(&mut self, exit: Exit, outputs: BTreeMap<String, Value>) -> Then {
        let Some(flow) = self.flows.last_mut() else {
            return Then::Stops;
        };
        let caller = flow.last().and_then(|call| call.caller);
        let Some(caller) = caller.filter(|_| flow.len() > 1) else {
            // A return in an event graph is a problem; the flow ends there.
            return self.finish();
        };
        flow.pop();
        if let Some(call) = flow.last_mut() {
            call.outputs
                .extend(outputs.into_iter().map(|(pin, value)| ((caller, pin), value)));
        }
        match exit {
            Exit::Done => self.go(DONE.id),
            Exit::Failed(reason) => self.failed(reason),
            Exit::Named(pin) => self.go(&pin),
        }
    }

    /// An unlinked Failed: the calls and chains up to the nearest try end and its Retry is told, through the calls
    /// of functions too (the owner, 2026-11-17: a function behaves in a try as its actions would there), else the
    /// character blocks with the reason, on the node that failed.
    fn tried(&mut self, reason: String) -> Then {
        let Some(flow) = self.flows.last() else {
            return Then::Stops;
        };
        let found = flow.iter().enumerate().rev().find_map(|(at, call)| {
            let chain = call.chains.iter().rposition(|chain| chain.body == Body::Try)?;
            Some((at, chain))
        });
        let Some((call_at, try_at)) = found else {
            return self.block(Reason::Failed(reason));
        };
        let Some(flow) = self.flows.last_mut() else {
            return Then::Stops;
        };
        flow.truncate(call_at + 1);
        let Some(call) = flow.last_mut() else {
            return Then::Stops;
        };
        call.chains.truncate(try_at);
        if let Some(chain) = call.chains.last_mut() {
            chain.resume = Resume::Failed(reason);
        }
        Then::Runs
    }

    /// Leave the loop: the chains up to the nearest loop's body end, and its owner is told.
    fn leave(&mut self) -> Then {
        let Some(call) = self.flows.last_mut().and_then(|flow| flow.last_mut()) else {
            return Then::Stops;
        };
        let Some(loop_at) = call.chains.iter().rposition(|chain| chain.body == Body::Loop) else {
            return self.block(Reason::Fault(Fault::NotInALoop));
        };
        call.chains.truncate(loop_at);
        if let Some(chain) = call.chains.last_mut() {
            chain.resume = Resume::Left;
        }
        Then::Runs
    }

    /// Finished: nothing waits any more.
    fn finish(&mut self) -> Then {
        self.state = State::Finished;
        self.wake = None;
        Then::Stops
    }

    fn block(&mut self, reason: Reason) -> Then {
        self.block_at(reason, None)
    }

    /// Blocked on `node` of the graph at work, or on the node at work.
    fn block_at(&mut self, reason: Reason, node: Option<usize>) -> Then {
        let (graph, at) = self.at();
        let node = node.unwrap_or(at);
        self.wake = None;
        let code = &self.program.graphs[graph];
        self.state = State::Blocked(Blocked {
            reason,
            graph: code.id.0.clone(),
            node: code.nodes[node].id.0.clone(),
        });
        Then::Stops
    }
}

/// Set: the variable takes its input's value, refused when out of its bounds, and gives it on.
fn set(cx: &mut Cx<'_>, variable: &str) -> Result<Step, Fault> {
    let value = cx.input("value")?;
    let local = cx.program.graphs[cx.graph].locals.iter().find(|it| it.id.0 == variable);
    match local {
        Some(local) if !local.accepts(&value) => {
            return Err(Fault::Refused {
                variable: variable.to_owned(),
            });
        }
        Some(_) => {
            cx.call.locals.insert(variable.to_owned(), value.clone());
        }
        None => cx.variables.set(variable, value.clone())?,
    }
    cx.give("value", value);
    Ok(Step::go("then"))
}
