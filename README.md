#ZFSMonitoringTools.NET

Pointless-but-cool little utilities that visualizes current disk activity of a ZFS disk array. Inspired by Pascal Gienger (http://southbrain.com/south/). Awesome stuff.

#USAGE

##ZFSSectorActivityThingie

Stick zfs1.sh (I need to find out who wrote this, I didn't) on a solaris box. It's a simple DTrace script that just chucks loads of disk access data out. Copy zfs-server.sh as well. This just pipes zfs1.sh through netcat on port 1234 and keeps it alive if the client disconnects. Run zfs-server.sh.
The .NET client can then connect over TCP to a host running this little server and display a window showing each disk and the read/write activity on that disk. Reads are green, writes are red. Simples.

##ZFSDiskIOPSActivityBarChart

Similar idea, but just use zpool iostat -v 1 instead of zfs1.sh to pipe into netcat.

#BUGS

Loads.