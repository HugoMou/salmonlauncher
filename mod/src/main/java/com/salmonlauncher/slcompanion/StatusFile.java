package com.salmonlauncher.slcompanion;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.JsonObject;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;

/**
 * Writes the small JSON status file that SalmonLauncher reads.
 *
 * <p>The target file is taken from the {@code -Dsl.link.status=<absolute path>} system property.
 * When the property is absent or unusable every call is a no-op.</p>
 */
public final class StatusFile {

    public static final String PATH_PROPERTY = "sl.link.status";

    private static final Gson GSON = new GsonBuilder().disableHtmlEscaping().create();
    private static Path path;
    private static boolean resolved;

    private StatusFile() {
    }

    private static Path path() {
        if (!resolved) {
            resolved = true;
            String raw = System.getProperty(PATH_PROPERTY);
            if (raw != null && !raw.isBlank()) {
                try {
                    Path candidate = Paths.get(raw).toAbsolutePath();
                    Path parent = candidate.getParent();
                    if (parent != null) {
                        Files.createDirectories(parent);
                    }
                    path = candidate;
                } catch (Exception e) {
                    SLCompanion.LOGGER.warn("[SLCompanion] unusable -D{} value '{}': {}", PATH_PROPERTY, raw, e.toString());
                }
            }
        }
        return path;
    }

    /** World is open; {@code port} is 0 while LAN has not been opened yet. */
    public static synchronized void writeReady(int port, String motd, int players, int maxPlayers) {
        JsonObject o = new JsonObject();
        o.addProperty("port", port);
        o.addProperty("motd", motd == null ? "" : motd);
        o.addProperty("players", players);
        o.addProperty("maxPlayers", maxPlayers);
        o.addProperty("ready", port > 0);
        o.addProperty("updatedAt", now());
        write(o);
    }

    /** No world is open. */
    public static synchronized void writeNotReady() {
        JsonObject o = new JsonObject();
        o.addProperty("ready", false);
        write(o);
    }

    private static String now() {
        return DateTimeFormatter.ISO_OFFSET_DATE_TIME.format(ZonedDateTime.now(ZoneOffset.UTC));
    }

    private static void write(JsonObject o) {
        Path target = path();
        if (target == null) {
            return;
        }
        String json = GSON.toJson(o);
        try {
            Path tmp = target.resolveSibling(target.getFileName().toString() + ".tmp");
            Files.writeString(tmp, json, StandardCharsets.UTF_8);
            try {
                Files.move(tmp, target, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
            } catch (Exception atomicFailed) {
                Files.writeString(target, json, StandardCharsets.UTF_8);
                try {
                    Files.deleteIfExists(tmp);
                } catch (Exception ignored) {
                    // best effort
                }
            }
        } catch (Exception e) {
            SLCompanion.LOGGER.warn("[SLCompanion] could not write status file {}: {}", target, e.toString());
        }
    }
}
