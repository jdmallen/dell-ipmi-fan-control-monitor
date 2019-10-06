# Dell IPMI Fan Control Monitor

OK I know it's not the best name, but in short, this is a .NET Core 3.0 service that monitors the temperature of a Dell PowerEdge R620 and automatically engages or disengages static fan control based on temperature, in order to control noise in a homelab.

While designed around a R620 (which is what I have), I believe it will work with the R610, R720, and R710. And probably lots others. YMMV.

## WARNING
This program sends automated, raw IPMI commands to your iDRAC device at regular intervals. As such, **USE AT YOUR OWN RISK**.

## Get started

1. Ensure target machine has `ipmitool` installed, and has access to the iDRAC device you wish to monitor/control. Test it with a simple command like `./ipmitool -I lanplus -H 10.10.1.2 -U root -P password sdr type temperature` where the host, user, and password are replaced as appropriate.
2. Install [.NET Core runtime](https://dotnet.microsoft.com/download) on your target machine, whether it's Windows or Linux.    
(Sorry, no Mac support yet, but I have a MBP coming soon and might add support later. It's just a few lines to support the process runner. I welcome pull requests.)
3. Build & publish for your target environment, then deploy to your target machine. (Yeah, sorry. I'll get some releases up soon. For now, you'll need the [.NET Core 3.0 SDK](https://dotnet.microsoft.com/download) on your user machine.)
4. Set `DOTNET_ENVIRONMENT` environment variable to `Production` on target machine.
5. (Optional) Install as Windows Services [using sc.exe](https://support.microsoft.com/en-au/help/251192/how-to-create-a-windows-service-by-using-sc-exe) or as [Systemd service](https://dejanstojanovic.net/aspnet/2018/june/setting-up-net-core-servicedaemon-on-linux-os/) (skip to "Service/Daemon setup").
6. Run it.
