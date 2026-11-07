package com.salmonlauncher.slcompanion;

import net.minecraft.server.MinecraftServer;

/**
 * Owns the "open to LAN" state of the singleplayer server and keeps the status file up to date.
 *
 * <p>All methods that touch the file are synchronized; the client tick thread drives
 * {@link #tick()}, player join/leave events may call in from the server thread.</p>
 */
public final class LanBridge {

    private static final long STATUS_INTERVAL_MS = 5_000L;
    private static final long LAN_RETRY_MS = 2_000L;
    private static final int[] FALLBACK_PORTS = {51234, 51235, 51236, 51237, 51238, 51239, 51240};

    /** Set by the mixin on the server thread when a world finished loading. */
    private static volatile boolean worldLoaded;

    private static MinecraftServer active;
    private static boolean lanOpened;
    private static int attempts;
    private static int fallbackIndex;
    private static long lastLanAttempt;
    private static long lastStatusWrite;
    private static boolean notReadyWritten;

    private LanBridge() {
    }

    /** Called from the mixin when {@code MinecraftServer#loadWorld} returns. */
    public static void markWorldLoaded() {
        worldLoaded = true;
    }

    public static synchronized void attach(MinecraftServer server) {
        if (server == null) {
            return;
        }
        if (active != server) {
            active = server;
            lanOpened = server.getServerPort() > 0;
            attempts = 0;
            fallbackIndex = 0;
            lastLanAttempt = 0L;
            lastStatusWrite = 0L;
            notReadyWritten = false;
            RoomBroadcast.reset();
        }
    }

    public static synchronized void detach() {
        if (active == null) {
            return;
        }
        active = null;
        lanOpened = false;
        attempts = 0;
        worldLoaded = false;
        RoomBroadcast.reset();
        if (!notReadyWritten) {
            notReadyWritten = true;
            StatusFile.writeNotReady();
        }
    }

    public static synchronized void tick() {
        MinecraftServer server = active;
        if (server == null) {
            return;
        }
        long now = System.currentTimeMillis();
        if (worldLoaded) {
            worldLoaded = false;
            lastLanAttempt = 0L;
            SLCompanion.LOGGER.info("[SLCompanion] singleplayer world finished loading, opening it to LAN");
        }
        try {
            ensureLanOpen(server, now);
            if (now - lastStatusWrite >= STATUS_INTERVAL_MS) {
                lastStatusWrite = now;
                pushStatus(server);
            }
            RoomBroadcast.tick(server);
        } catch (Throwable t) {
            SLCompanion.LOGGER.warn("[SLCompanion] tick failed: {}", t.toString());
        }
    }

    private static void ensureLanOpen(MinecraftServer server, long now) {
        int port = server.getServerPort();
        if (port > 0) {
            if (!lanOpened) {
                lanOpened = true;
                lastStatusWrite = 0L;
            }
            return;
        }
        // isRemote() is true for the client-side integrated server, false for a dedicated one.
        if (!server.isRemote()) {
            return;
        }
        if (now - lastLanAttempt < LAN_RETRY_MS) {
            return;
        }
        lastLanAttempt = now;
        attempts++;

        int requested = 0;
        if (attempts > 3) {
            requested = FALLBACK_PORTS[fallbackIndex];
            fallbackIndex = (fallbackIndex + 1) % FALLBACK_PORTS.length;
        }
        try {
            server.openToLan(server.getDefaultGameMode(), server.getPlayerManager().areCheatsAllowed(), requested);
        } catch (Throwable t) {
            SLCompanion.LOGGER.warn("[SLCompanion] openToLan attempt {} failed: {}", attempts, t.toString());
        }

        port = server.getServerPort();
        if (port > 0) {
            lanOpened = true;
            lastStatusWrite = 0L;
            pushStatus(server);
            SLCompanion.LOGGER.info("[SLCompanion] world is now open to LAN on port {}", port);
        } else {
            SLCompanion.LOGGER.debug("[SLCompanion] LAN not open yet (attempt {})", attempts);
        }
    }

    public static synchronized void pushStatus(MinecraftServer server) {
        if (server == null) {
            return;
        }
        try {
            StatusFile.writeReady(server.getServerPort(), worldName(server),
                    server.getPlayerManager().getCurrentPlayerCount(),
                    server.getPlayerManager().getMaxPlayerCount());
        } catch (Throwable t) {
            SLCompanion.LOGGER.warn("[SLCompanion] could not publish status: {}", t.toString());
        }
    }

    private static String worldName(MinecraftServer server) {
        try {
            return server.getSaveProperties().getLevelName();
        } catch (Throwable t) {
            return "";
        }
    }

    public static void onPlayerCountChanged(MinecraftServer server) {
        if (server == null || !server.isRemote()) {
            return;
        }
        pushStatus(server);
    }

    public static void onServerStopped(MinecraftServer server) {
        synchronized (LanBridge.class) {
            if (server != null && active == server) {
                detach();
            }
        }
    }
}
