package com.salmonlauncher.slcompanion;

import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.server.IntegratedServer;

/**
 * Client entry point: drives {@link LanBridge} every client tick.
 */
public final class SLCompanionClient implements ClientModInitializer {

    @Override
    public void onInitializeClient() {
        ClientTickEvents.END_CLIENT_TICK.register(SLCompanionClient::onEndClientTick);
    }

    private static void onEndClientTick(MinecraftClient client) {
        try {
            IntegratedServer server = client.getServer();
            if (server == null) {
                LanBridge.detach();
                return;
            }
            LanBridge.attach(server);
            LanBridge.tick();
        } catch (Throwable t) {
            SLCompanion.LOGGER.warn("[SLCompanion] client tick failed: {}", t.toString());
        }
    }
}
