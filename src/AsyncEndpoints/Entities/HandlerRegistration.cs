using System;

namespace AsyncEndpoints.Entities;

public sealed class HandlerRegistration(string jobName, Type requestType, Type responseType)
{
    public string JobName { get; set; } = jobName;
    public Type RequestType { get; set; } = requestType;
    public Type ResponseType { get; set; } = responseType;
}
