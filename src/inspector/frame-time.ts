const pad = (value: number, width: number) => String(value).padStart(width, "0");

/** A frame's time since its connection opened, as `mm:ss.mmm`, with the hours before once there are some. */
export function frameTime(millis: number): string {
  const hours = Math.floor(millis / 3_600_000);
  const minutes = Math.floor(millis / 60_000) % 60;
  const seconds = Math.floor(millis / 1000) % 60;
  const time = `${pad(minutes, 2)}:${pad(seconds, 2)}.${pad(millis % 1000, 3)}`;
  return hours > 0 ? `${hours}:${time}` : time;
}
