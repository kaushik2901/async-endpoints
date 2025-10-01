# AsyncEndpoints backlog

## Pending

- AsyncEndpoints must not return serialized result as part of job response
- AsyncEndpoints should return proper Result.Problem in case of error
- AsyncEndpoints should support both AOT and JIT compilation
- AsyncEndpoints should have extension to register handlers via assembly scanning (using reflection)
- AsyncEndpoints should support EF core as job storage
- AsyncEndpoints should not have delay due to cold start
- AsyncEndpoints should allow developers to configure custom response (both success and error)

## In progress

## Completed

- MethodResult.Data must not be nullable in case of success result
- MethodResult.Error must not be nullable in case of failure result
- MethodResult should not have exception property, it should parse exception properly and convert to AsyncEndpointError object