using System;
using System.Collections.Generic;
using System.Text;

namespace DsgOmnichannel.Domain.Events;

public record PingEvent(Guid Id, string Message, DateTime Timestamp);