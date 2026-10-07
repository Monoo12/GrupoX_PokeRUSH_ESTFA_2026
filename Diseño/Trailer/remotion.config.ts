/**
 * Note: When using the Node.JS APIs, the config file
 * doesn't apply. Instead, pass options directly to the APIs.
 *
 * All configuration options: https://remotion.dev/docs/config
 */

import { Config } from "@remotion/cli/config";

Config.setRspack(true);
// PNG y no JPEG: el píxel art y los textos chicos no se ensucian antes del H.264
Config.setVideoImageFormat("png");
Config.setOverwriteOutput(true);
Config.setCrf(16);
