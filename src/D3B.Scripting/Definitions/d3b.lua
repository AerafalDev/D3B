---@meta d3b
-- D3B's Lua API, as LuaCATS annotations: the Lua extension of VS Code (LuaLS) reads them for documentation and
-- completion. D3B writes this file into the scripts' folder each time its host starts: edit your scripts, not this file.
--
-- A script is a .lua file of the folder. Its text runs once, when an activity starts, to define its hooks and declare
-- its settings; it cannot act, the bot reaching the hooks alone. The bot then calls step(bot) again and again, each step
-- chosen from the character's state as it is then. Every action waits until it is over and answers as Lua's own
-- functions do: true, or false and why.
--
--     settings = {
--       { key = "walks", kind = "number", name = { fr = "Marches", en = "Walks" }, min = 1, max = 100, default = 10 },
--     }
--
--     local walked = 0
--
--     function step(bot)
--       local ok, why = bot:walk(301)
--       if not ok then log.error("no walk: " .. why) end
--       walked = walked + 1
--       return walked < settings.walks
--     end

-- Hooks ---------------------------------------------------------------------------------------------------------------

---Runs one step, chosen from the character's state now. Required.
---An interruption (the player acting, the connection closing, a fight) cancels the action it waits on and the step
---with it; once the bot can act again and is recovered, the next step starts from the state, the script's variables as
---they were. pcall cannot catch an interruption.
---@param bot D3BBot
---@return boolean|nil more false to end the activity; anything else goes on
function step(bot) end

---Tells whether the character carries too much to go on, before each step; then unload(bot) runs instead. Optional.
---It reads the bot and never acts.
---@param bot D3BBot
---@return boolean full
function isFull(bot) end

---Unloads a full character, such as at its bank. Optional: without it, a full character ends the activity.
---@param bot D3BBot
---@return boolean unloaded true once the character is unloaded; still full after, the activity fails
function unload(bot) end

---Tells what an interruption leads to, once it cancelled a step. Optional: by default the activity resumes.
---@param reason D3BInterruption
---@return "stop"|"resume"|nil answer "stop" ends the activity
function onInterrupted(reason) end

---Puts the character back in order after an interruption, before the next step, once D3B's own recovery let a walk or a
---use end and left a dialog. Optional.
---@param bot D3BBot
---@param reason D3BInterruption
function recover(bot, reason) end

---@alias D3BInterruption
---| "player"       # the player acted on the character; the bot paused until resumed
---| "disconnected" # the game connection closed; a new one of the client took the bot back
---| "fight"        # a fight began; it is over
---| "stopped"      # the activity or the host is stopping

-- Settings ------------------------------------------------------------------------------------------------------------

---The script's settings. As the text runs, declare them here, one entry per setting in the order the app shows them,
---and a profile edits them. Once the text ran, D3B gives this global their values instead, by key: settings.walks.
---@type (D3BSetting|D3BSection)[]|table<string, any>
settings = {}

---@alias D3BText string|table<string, string> # a text, or texts by language: { fr = "...", en = "..." }

---@class D3BSection
---@field section D3BText the title the next settings show under

---@class D3BSetting
---@field key string the setting's key, under which its value is stored and read
---@field kind D3BSettingKind what it holds
---@field name? D3BText its name in the app; its key by default
---@field help? D3BText what the app says below it
---@field default? any its default: false, the lowest number, "", the first option, nothing, or an empty list
---@field min? integer a number's lowest value
---@field max? integer a number's highest value
---@field maxLength? integer a text's greatest length; 100 by default
---@field options? (string|{ value: string, name: D3BText })[] the values a choice offers
---@field required? boolean asks for a value: a text not blank, a value of the game, a list not empty
---@field ordered? boolean a list whose order matters, such as a priority
---@field within? string the key of the setting a sub-area is sought within (an area), or a resource (a job)

---@alias D3BSettingKind
---| "flag"      # true or false
---| "number"    # a whole number between min and max
---| "text"      # a string of at most maxLength
---| "choice"    # one of the options
---| "choices"   # several of the options
---| "job"       # a profession, by id
---| "jobs"      # professions
---| "area"      # an area of the world, by id
---| "areas"     # areas
---| "subArea"   # a sub-area, by id, within an area
---| "subAreas"  # sub-areas
---| "resource"  # a resource a profession gathers, by its item's id, within a job
---| "resources" # resources

-- The bot -------------------------------------------------------------------------------------------------------------

---The bot a hook receives: its fields read the character's state as it is when read, its methods act and wait.
---@class D3BBot
---@field character { id: integer, name: string, level: integer? }
---@field map { id: integer, x: integer?, y: integer?, worldMap: integer?, subArea: integer? }? the map it stands on
---@field cell integer? the cell it stands on
---@field inventory { weight: integer, maxWeight: integer, load: number, kamas: integer, items: { uid: integer, item: integer, quantity: integer, worn: boolean }[] } load from 0 to 1
---@field jobs table<integer, { id: integer, level: integer, experience: integer }> its professions, by id
---@field isBusy boolean walking, using an element, in a dialog or a fight
---@field isWalking boolean
---@field isUsing boolean using an element, such as harvesting
---@field isInDialog boolean with an NPC, the bank or a zaap
---@field isFighting boolean
---@field isInHavenBag boolean
---@field isSubscribed boolean its account's subscription runs
local D3BBot = {}

---Walks to a cell of the map.
---@param cell integer from 0 to 559
---@return boolean ok
---@return "busy"|"noPath"|"refused"|"noAnswer"|"notConfirmed"|nil why
function D3BBot:walk(cell) end

---Leaves the map by a side, or by the map action of a cell.
---@param where "top"|"bottom"|"left"|"right"|integer
---@return boolean ok
---@return "elsewhere"|"busy"|"noPath"|"notWalked"|"refused"|"noAnswer"|"notPlaced"|nil why
function D3BBot:changeMap(where) end

---Travels to a map, or to the nearest of several, through the world: scrolls, doors, map actions and zaaps.
---@param maps integer|integer[] map ids
---@return boolean ok
---@return "busy"|"noPath"|"failed"|nil why
function D3BBot:travel(maps) end

---Uses an element of the map with one of its skills, such as harvesting a tree, from the nearest cell it is used from.
---@param element integer the element's id on the map
---@param skill integer the skill's id
---@return boolean ok
---@return "busy"|"unavailable"|"noPath"|"notWalked"|"refused"|"noAnswer"|"notEnded"|nil why
function D3BBot:use(element, skill) end

---Goes to the nearest bank and puts in the stacks the character does not wear, every one or those of some items, and kamas.
---@param items? integer[] item ids; every stack not worn by default
---@param kamas? integer 0 by default
---@return boolean ok
---@return "busy"|"noBank"|"notTravelled"|"refused"|"noAnswer"|nil why
function D3BBot:bank(items, kamas) end

---Leaves the haven bag the character is in, back to the map it entered it from.
---@return boolean ok
---@return string? why
function D3BBot:leaveHavenBag() end

---Waits, by the bot's clock.
---@param milliseconds integer at most an hour
function D3BBot:wait(milliseconds) end

---Waits until the character is not busy, at most a delay.
---@param milliseconds? integer 10 s by default
---@return boolean idle false when the delay passed first
function D3BBot:waitIdle(milliseconds) end

---Counts the objects of an item the character carries.
---@param item integer the item's id
---@return integer count
function D3BBot:count(item) end

---Gives a profession's level, 0 when the character has not learnt it.
---@param job integer the profession's id
---@return integer level
function D3BBot:jobLevel(job) end

---Sends the game server a request of D3B's readable protocol, whole, its fields in lower camel case, checked.
---The client is not told: a request it does not follow may leave it out of step with the server.
---@param name string such as "MapMovementRequest"
---@param fields? table such as { mapId = 191104002, keyMovements = { 300, 301 } }
---@return boolean ok
function D3BBot:send(name, fields) end

---Waits for the next event or response of D3B's readable protocol from the server, from now on.
---@param name string such as "KamasUpdateEvent"
---@param milliseconds? integer 10 s by default
---@return table|nil message its fields the readable protocol reads, an enumeration's value by name
---@return "timeout"|nil why
function D3BBot:waitFor(name, milliseconds) end

---Sends a request and waits for its answer, which it expects before sending, so that an answer at once is not missed.
---@param name string the request
---@param fields table|nil its fields
---@param answer string the event or response that answers it
---@param milliseconds? integer 10 s by default
---@return table|nil message
---@return "timeout"|nil why
function D3BBot:request(name, fields, answer, milliseconds) end

-- Globals -------------------------------------------------------------------------------------------------------------

---What the script tells its console, which the bot's log keeps too.
log = {}

---@param ... any joined by tabs, as print does
function log.info(...) end

---@param ... any
function log.success(...) end

---@param ... any
function log.error(...) end

---The installed build's data, which D3B's pack holds.
game = {}

---Gives the maps at coordinates, of every world map.
---@param x integer
---@param y integer
---@return integer[] maps
function game.mapsAt(x, y) end

---Gives where a map lies.
---@param map integer
---@return { x: integer, y: integer, subArea: integer, worldMap: integer }|nil position
function game.position(map) end

---Gives the distance between two cells of a map, as the server measures a skill's range.
---@param from integer
---@param to integer
---@return integer distance
function game.distance(from, to) end

---Describes a cell of a map.
---@param map integer
---@param cell integer
---@return { walkable: boolean, zone: integer }|nil cell nil for a map the pack lacks
function game.cell(map, cell) end

---Gives the cells of a map whose map action sends whoever steps on them to another map.
---@param map integer
---@return integer[] cells
function game.mapActions(map) end
