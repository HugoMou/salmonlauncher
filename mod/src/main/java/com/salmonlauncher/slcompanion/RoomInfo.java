package com.salmonlauncher.slcompanion;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import net.minecraft.text.MutableText;
import net.minecraft.text.Text;

import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;

/**
 * Parses the optional room description file written by SalmonLauncher.
 *
 * <p>The path comes from the {@code -Dsl.link.room=<absolute path>} system property. The file
 * contains {@code {"roomName":"...","hostName":"...","players":["a","b"]}}; its content is turned
 * into a tab list header/footer.</p>
 */
public final class RoomInfo {

    public static final String PATH_PROPERTY = "sl.link.room";

    private static Path path;
    private static boolean resolved;

    private RoomInfo() {
    }

    /** @return the configured room file path, or {@code null} when the property is absent. */
    public static Path path() {
        if (!resolved) {
            resolved = true;
            String raw = System.getProperty(PATH_PROPERTY);
            if (raw != null && !raw.isBlank()) {
                try {
                    path = Paths.get(raw).toAbsolutePath();
                } catch (Exception e) {
                    SLCompanion.LOGGER.warn("[SLCompanion] unusable -D{} value '{}': {}", PATH_PROPERTY, raw, e.toString());
                }
            }
        }
        return path;
    }

    /**
     * Builds {@code [header, footer]} from the raw JSON content.
     *
     * @return {@code null} when the content cannot be parsed.
     */
    public static Text[] build(String raw) {
        try {
            JsonElement root = JsonParser.parseString(raw);
            if (!root.isJsonObject()) {
                return null;
            }
            JsonObject o = root.getAsJsonObject();
            String roomName = optString(o, "roomName");
            String hostName = optString(o, "hostName");
            List<String> players = optStringList(o, "players");

            MutableText header = Text.literal("§6§lSL Room §r§e" + (roomName.isEmpty() ? "Singleplayer" : roomName));
            if (!hostName.isEmpty()) {
                header.append(Text.literal("\n§7Host §f" + hostName));
            }
            MutableText footer = Text.literal("§7Players §f" + (players.isEmpty() ? "-" : String.join(", ", players)));
            footer.append(Text.literal("\n§8SalmonLauncher"));
            return new Text[]{header, footer};
        } catch (Exception e) {
            SLCompanion.LOGGER.warn("[SLCompanion] could not parse room file content: {}", e.toString());
            return null;
        }
    }

    private static String optString(JsonObject o, String key) {
        JsonElement e = o.get(key);
        if (e == null || e.isJsonNull()) {
            return "";
        }
        try {
            return e.getAsString();
        } catch (Exception ignored) {
            return "";
        }
    }

    private static List<String> optStringList(JsonObject o, String key) {
        List<String> out = new ArrayList<>();
        JsonElement e = o.get(key);
        if (e == null || !e.isJsonArray()) {
            return out;
        }
        JsonArray array = e.getAsJsonArray();
        for (JsonElement element : array) {
            if (element != null && !element.isJsonNull()) {
                try {
                    out.add(element.getAsString());
                } catch (Exception ignored) {
                    // skip malformed entry
                }
            }
        }
        return out;
    }
}
