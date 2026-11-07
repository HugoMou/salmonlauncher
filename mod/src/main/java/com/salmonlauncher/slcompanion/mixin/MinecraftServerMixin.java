package com.salmonlauncher.slcompanion.mixin;

import com.salmonlauncher.slcompanion.LanBridge;
import net.minecraft.server.MinecraftServer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/**
 * Hooks the end of {@code MinecraftServer#loadWorld()} - the point where the (integrated) server
 * has finished loading its worlds - and asks {@link LanBridge} to open the singleplayer world to
 * LAN. The actual call happens on the client thread from the next client tick, mirroring what the
 * vanilla "Open to LAN" button does.
 */
@Mixin(MinecraftServer.class)
public abstract class MinecraftServerMixin {

    @Inject(method = "loadWorld", at = @At("RETURN"))
    private void slcompanion$afterWorldLoaded(CallbackInfo ci) {
        if (((MinecraftServer) (Object) this).isRemote()) {
            LanBridge.markWorldLoaded();
        }
    }
}
