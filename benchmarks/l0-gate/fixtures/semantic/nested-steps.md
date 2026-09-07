# Cutover

`relay attach`

Prepare the host, then install, then verify.

1. Prepare the host
   1. Confirm disk free space is at least 2 GB.
   2. Stop the previous worker with `systemctl stop occam`.
2. Install the new build
   1. Copy the tarball into `/opt/occam`.
   2. Run `occam connect` after PATH is updated.
3. Verify `tools/list` returns the reader profile.

## Weather

Bring an umbrella if the forecast shows rain.
Gardeners discuss soil temperature, compost ratios, and evening watering
schedules that have nothing to do with host cutover or MCP registration.
Leave this section out of a focused install answer.
