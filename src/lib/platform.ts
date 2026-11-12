/** The desktop system the webview runs on, read from its user agent: Tauri's webviews name it as browsers do. */
export type Platform = "windows" | "mac" | "linux";

/** The platform of a user agent; anything neither Windows nor macOS is treated as Linux. */
export function platformOf(userAgent: string): Platform {
  if (userAgent.includes("Windows")) {
    return "windows";
  }
  if (userAgent.includes("Mac OS X") || userAgent.includes("Macintosh")) {
    return "mac";
  }
  return "linux";
}

/** The platform this webview runs on. */
export function platform(): Platform {
  return platformOf(navigator.userAgent);
}
