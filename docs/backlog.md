# AsyncEndpoints backlog

## Pending

- Fix all the issues from exception-handling-analysis.md
- AsyncEndpoints should support both AOT and JIT compilation
- AsyncEndpoints should record individual job runs
- AsyncEndpoints should support EF core as job storage
- AsyncEndpoints should support request without body
- AsyncEndpoints should support PUT, PATCH, and DELETE methods
- AsyncEndpoints should have extension to register handlers via assembly scanning (using reflection)
- AsyncEndpoints should not have delay due to cold start

## In progress


## Completed

- AsyncEndpoints.Redis store is not picking existing jobs after restart
- AsyncEndpoints must release in progress job after certain time
- MethodResult.Data must not be nullable in case of success result
- MethodResult.Error must not be nullable in case of failure result
- MethodResult should not have exception property, it should parse exception properly and convert to AsyncEndpointError object
- AsyncEndpoints should return proper Result.Problem in case of error
- AsyncEndpoints should allow developers to configure custom response (both success and error)
- AsyncEndpoints must not return serialized result as part of job response
- Ensure all interface have proper documentation comments
- Write unit tests for RedisLuaScriptService
