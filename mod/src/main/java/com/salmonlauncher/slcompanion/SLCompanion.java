package com.salmonlauncher.slcompanion;

import net.fabricmc.api.ModInitializer;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents;
import net.fabricmc.fabric.api.networking.v1.ServerPlayConnectionEvents;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Common entry point. Registers the server side bookkeeping: player joins/leaves refresh the
 * status file, a stopping server publishes {@code {"ready":false}}.
 */
public final class SLCompanion implements ModInitializer {

    public static final String MOD_ID = "slcompanion";
    public static final Logger LOGGER = LoggerFactory.getLogger("SLCompanion");

    @Override
    public void onInitialize() {
        ServerPlayConnectionEvents.JOIN.register((handler, sender, server) -> {
            if (server == null || server.isRemote()) {
                return;
            }
            LanBridge.onPlayerCountChanged(server);
            RoomBroadcast.reapply(server);
        });

        ServerPlayConnectionEvents.DISCONNECT.register((handler, server) -> {
            if (server == null || server.isRemote()) {
                return;
            }
            LanBridge.onPlayerCountChanged(server);
        });

        ServerLifecycleEvents.SERVER_STOPPED.register(server -> {
            if (server == null || server.isRemote()) {
                return;
            }
            LanBridge.onServerStopped(server);
        });

        LOGGER.info("[SLCompanion] initialized; status path = {}, room path = {}",
                System.getProperty(StatusFile.PATH_PROPERTY, "<unset>"),
                System.getProperty(RoomInfo.PATH_PROPERTY, "<unset>"));
    }
}
