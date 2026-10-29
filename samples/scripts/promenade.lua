-- The stroll: walks the character, as many times as its settings say, to a cell near it chosen at random on its map,
-- away from the cells whose map action would send it to another map. D3B's former sample plugin, written as a script.
--
-- Each step is chosen from the state when it starts: after an interruption, the stroll goes on from wherever the
-- character stands. A walk the character is too busy for comes back at once: the stroll waits for it to be idle instead.

settings = {
  {
    key = "walks",
    kind = "number",
    name = { fr = "Marches", en = "Walks" },
    help = { fr = "Combien de marches la promenade fait.", en = "How many walks the stroll takes." },
    min = 1,
    max = 100,
    default = 10,
  },
  {
    key = "reach",
    kind = "number",
    name = { fr = "Portée", en = "Reach" },
    help = { fr = "Jusqu'à combien de cases va chaque marche.", en = "How many cells away each walk goes at most." },
    min = 1,
    max = 8,
    default = 4,
  },
}

local walked = 0

-- The cells within reach of where the character stands, walkable, in its zone, and on no map action.
local function destinations(map, from)
  local leaving = {}

  for _, cell in ipairs(game.mapActions(map)) do
    leaving[cell] = true
  end

  local zone = game.cell(map, from).zone
  local cells = {}

  for cell = 0, 559 do
    local here = game.cell(map, cell)

    if cell ~= from and not leaving[cell] and here.walkable and here.zone == zone and game.distance(from, cell) <= settings.reach then
      cells[#cells + 1] = cell
    end
  end

  return cells
end

function step(bot)
  local cells = destinations(bot.map.id, bot.cell)

  -- Nowhere to go: the stroll is over.
  if #cells == 0 then
    return false
  end

  local cell = cells[math.random(#cells)]
  local ok, why = bot:walk(cell)

  if why == "busy" then
    bot:waitIdle(5000)

    return true
  end

  walked = walked + 1
  log.info(string.format("walk %d of %d to cell %d: %s", walked, settings.walks, cell, ok and "arrived" or why))

  return walked < settings.walks
end
