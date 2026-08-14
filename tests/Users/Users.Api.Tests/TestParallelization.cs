// The API integration tests share process-global state: the Auth__CmsUsername and
// Auth__AdministratorUsername environment variables are temporarily mutated by one startup test while
// every host build reads them, so collections must run sequentially to avoid cross-test interference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
