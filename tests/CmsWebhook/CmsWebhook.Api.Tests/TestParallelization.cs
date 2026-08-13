// The API integration tests share process-global state: the Auth__CmsUsername environment variable is
// temporarily mutated by one auth test while every host build reads it, so collections must run
// sequentially to avoid cross-test interference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
