using Secs4Net;

namespace Secs;

public delegate Task SecsHandle(SecsMessage message, string data);

public delegate void SecsLevelHandle(string data);
