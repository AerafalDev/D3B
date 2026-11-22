## D499: Renew the Minor Uchronic Elixir while hunting

*2026-11-22, fight profiles, contributor request. Builds on D148 and D297.*

- A hunt profile can opt into using one carried Minor Uchronic Elixir (item 20962) when alteration 50 is missing or has zero fights remaining. Checking at startup covers profiles started midway through a session. After expiry the hunt sends one use and continues. The option defaults to off.
- The November 22 game sessions beginning at 13:19 and 14:04 UTC on 3.7.14.18 supplied the evidence. At entry and between fights a snapshot carries a repeated alteration record. The grant following use of the elixir carries one record of the same type. Its second field identifies the alteration and its third field counts down from 15 to zero. The expiry coincides with information 1665. A record can still be present at zero.
- The character keeps the durations by alteration ID. A grant can arrive before the first complete snapshot. The hunt distinguishes an unknown state from a reported absence and leaves its startup wait bounded.
- **Rejected:** trying a potion after every fight would send unnecessary use requests throughout the effect's duration.
