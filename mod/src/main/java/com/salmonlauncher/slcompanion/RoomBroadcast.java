package com.salmonlauncher.slcompanion;

import net.minecraft.network.packet.s2c.play.PlayerListHeaderS2CPacket;
import net.minecraft.server.MinecraftServer;
import net.minecraft.text.Text;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

/**
 * Polls the optional room file and mirrors its content into the tab list header/footer.
 *
 * <p>Vanilla has no server side "set header/footer" call, so the text is delivered with the same
 * {@link PlayerListHeaderS2CPacket} vanilla clients understand. When the file (or the
 * {@code -Dsl.link.room} property) is missing, nothing happens at all - the tab list is left
 * untouched.</p>
 */
public final class RoomBroadcast {

    private static final long CHECK_INTERVAL_MS = 1_000L;

    private static String lastAppliedRaw;
    private static Text lastHeader;
    private static Text lastFooter;
    private static long lastCheckMs;

    private RoomBroadcast() {
    }

    public static synchronized void reset() {
        lastAppliedRaw = null;
        lastHeader = null;
        lastFooter = null;
        lastCheckMs = 0L;
    }

    /** Called from the client tick loop roughly every second. */
    public static synchronized void tick(MinecraftServer server) {
        Path file = RoomInfo.path();
        if (file == null || server == null) {
            return;
        }
        long now = System.currentTimeMillis();
        if (now - lastCheckMs < CHECK_INTERVAL_MS) {
            return;
        }
        lastCheckMs = now;

        String raw;
        try {
            if (!Files.isRegularFile(file)) {
                return;
            }
            raw = Files.readString(file, StandardCharsets.UTF_8);
        } catch (Exception e) {
            return;
        }
        if (raw.equals(lastAppliedRaw)) {
            return;
        }
        Text[] headerFooter = RoomInfo.build(raw);
        if (headerFooter == null) {
            return;
        }
        lastAppliedRaw = raw;
        apply(server, headerFooter[0], headerFooter[1]);
    }

    /** Re-sends the last room text, e.g. after a player joined so they see it too. */
    public static synchronized void reapply(MinecraftServer server) {
        if (server == null || lastHeader == null) {
            return;
        }
        apply(server, lastHeader, lastFooter);
    }

    private static void apply(MinecraftServer server, Text header, Text footer) {
        lastHeader = header;
        lastFooter = footer;
        try {
            server.execute(() -> {
                try {
                    server.getPlayerManager().sendToAll(new PlayerListHeaderS2CPacket(header, footer));
                } catch (Throwable t) {
                    SLCompanion.LOGGER.warn("[SLCompanion] could not update tab list header/footer: {}", t.toString());
                }
            });
        } catch (Throwable t) {
            SLCompanion.LOGGER.warn("[SLCompanion] could not schedule tab list update: {}", t.toString());
        }
    }
}
