# Changelog

## [2.1.0] - 2025-03-26
### Changed
- Update packages:
  Dapper                                             1.60.1 -> 2.1.66
  Newtonsoft.Json                                    12.0.1 -> 13.0.3
  System.ComponentModel.Annotations                  4.5.0  -> 5.0.0
  Microsoft.Extensions.Configuration                 2.2.0  -> 9.0.3
  Microsoft.Extensions.Configuration.FileExtensions  2.2.0  -> 9.0.3
  Microsoft.Extensions.Configuration.Json            2.2.0  -> 9.0.3
  Microsoft.NET.Test.Sdk                             15.8.0 -> 17.13.0
  xunit                                              2.3.1  -> 2.9.3
  xunit.runner.visualstudio                          2.3.1  -> 3.0.2

## [2.0.0] - 2024-12-12

- Drop usage of System.Data.SqlClient in favor of Microsoft.Data.SqlClient
- Bump target framework from net451 to net462
- Self signed certificates are not accepted by default anymore
- BulkInsert operation returns now long instead of int
