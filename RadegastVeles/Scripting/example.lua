-- Radegast Veles Example Lua Script
-- Copy this file to ~/.config/RadegastVeles/scripts/
-- or ~/.local/share/RadegastVeles/scripts/

-- Called when the script starts
on_start = function()
    log("Hello from Lua script!")
end

-- Called when the script stops
on_stop = function()
    log("Goodbye from Lua script!")
end

-- Called when connected to the grid
on_connected = function()
    log("Connected to grid!")
    send_chat("Hello world from Lua!", 0)
end

-- Called when disconnected from the grid
on_disconnected = function()
    log("Disconnected from grid")
end

-- Called when a chat message is received
-- Parameters: message, type (ChatType int), sender_name
on_chat = function(msg, chat_type, sender)
    if chat_type == 0 then
        log(string.format("Chat from %s: %s", sender, msg))
    end
end

-- Called when an IM is received
-- Parameters: agent_id, sender_name, message
on_im = function(agent_id, sender, msg)
    log(string.format("IM from %s: %s", sender, msg))
end

-- Called after teleport completes
-- Parameters: region_name, x, y, z
on_teleport = function(region, x, y, z)
    log(string.format("Teleported to %s at %.1f, %.1f, %.1f", region, x, y, z))
end
