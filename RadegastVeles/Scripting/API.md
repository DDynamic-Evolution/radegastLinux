# Lua Scripting API — Radegast Veles

Place `.lua` files in `~/.local/share/RadegastVeles/scripts/`. They are loaded automatically on startup and start when connected to a grid.

## Lifecycle Hooks

Define these as global functions in your script. They are called automatically by the client.

```lua
function on_start()
    -- Called when the script is loaded and started (on connect or reload)
end

function on_stop()
    -- Called when the script is stopped (on disconnect or reload)
end

function on_connected()
    -- Called after successfully connecting to a grid
end

function on_disconnected()
    -- Called after disconnecting from the grid
end
```

## Event Hooks

These receive event data from the client.

```lua
function on_chat(message, chat_type, sender_name)
    -- message:    string   the chat text
    -- chat_type:  number   ChatType enum value
    --                       0 = Normal
    --                       1 = Whisper
    --                       2 = Shout
    --                       4 = OwnerSay
    -- sender_name: string   display name of the sender
end

function on_im(agent_id, sender_name, message)
    -- agent_id:    string   UUID of the sender
    -- sender_name: string   display name of the sender
    -- message:     string   IM text
end

function on_teleport(region, x, y, z)
    -- region: string   destination region name
    -- x, y, z: number  position in the region
end
```

## Action Functions

Call these from anywhere in your script.

### send_chat
```lua
send_chat("Hello world!", 0)
-- Sends a chat message on the given channel
```

### send_im
```lua
send_im("a1b2c3d4-...", "Hello via IM!")
-- Sends an instant message to the given agent UUID
```

### teleport
```lua
teleport("Region Name", 128, 128, 50)
-- Teleports the avatar to a region at the given position
```

### log / log_info / log_warn / log_error
```lua
log("plain message")
log_info("informational")
log_warn("something suspicious")
log_error("something failed")
-- All four appear in the Scripting tab console in Preferences
-- and in the client log output
```

### get_setting / set_setting
```lua
local val = get_setting("my_key")  -- returns string or nil
set_setting("my_key", "my_value")
-- Reads/writes a key in the client's GlobalSettings store
-- (persisted between sessions)
```

### http_get
```lua
http_get("https://example.com/api/data", function(body, err)
    if err then
        log_error("HTTP error: " .. err)
        return
    end
    log("Response: " .. body)
end)
-- Makes an HTTP GET request. The callback receives (body, error).
-- Only one of body or error will be non-nil.
```

### schedule
```lua
schedule(5.0, function()
    log("5 seconds have passed!")
end)
-- Calls the function after the given delay in seconds.
-- Scheduled callbacks are checked every 500ms on the UI thread.
```

## Lifecycle

| Event | When |
|---|---|
| Script file discovered | On startup, or when "Reload All" is clicked |
| `on_start` / hooks registered | After `DoFile`, when connected to grid |
| `on_connected` | Fired once after login completes |
| `on_chat` / `on_im` | Each time a matching message arrives |
| `on_teleport` | After a teleport finishes successfully |
| `on_disconnected` | On logout / disconnect |
| `on_stop` | Before the script is unloaded |
| Script reloaded | Via "Reload Selected" / "Reload All" in Preferences |

## Notes

- Scripts run in a MoonSharp sandbox (`CoreModules.Preset_SoftSandbox`).
- Access to `os.execute`, `io.*` file writes, `require` (outside the script dir) etc. is blocked.
- Scripts cannot block the UI thread; callbacks are dispatched on the Avalonia UI thread.
- Use `schedule()` for delayed or periodic work; don't use busy loops.
- The scripts directory is created automatically if it doesn't exist.
