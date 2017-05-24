[TOC]

# Task documentation #

## Sql.ExecuteQuery ##
### Input ###
| Property          | Type                              | Description                                             | Example                                   |
|-------------------|-----------------------------------|---------------------------------------------------------|-------------------------------------------|
| Query             | string                            | The query that will be executed to the database.        | `select Name,Age from MyTable where AGE = @Age` 
| Parameters        | Array{Name: string, Value: string} | A array of parameters to be appended to the query.     | `Name = Age, Value = 42`
| Connection String | string                            | Connection String to be used to connect to the database.| `Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;`


### Options ###
| Property               | Type                 | Description                                                |
|------------------------|----------------------|------------------------------------------------------------|
| Command Timeout        | int                  | Timeout in seconds to be used for the query. 60 seconds by default. |
| Sql Transaction Isolation Level | SqlTransationIsolationLevel | Transactions specify an isolation level that defines the degree to which one transaction must be isolated from resource or data modifications made by other transactions. Possible values are: Default, None, Serializable, ReadUncommitted, ReadCommitted, RepeatableRead, Snapshot. Additional documentation https://msdn.microsoft.com/en-us/library/ms378149(v=sql.110).aspx |

### Result ###
The result of the task will always be of type json Array.

Example result
```
#!json
[ 
 {
  "Name": "Foo",
  "Age": 42
 },
 {
  "Name": "Adam",
  "Age": 42
 }
]
```
```
#!text
The second name 'Adam' can be now be accessed by #result[1].Name in the process parameter editor.

```


## Sql.ExecuteProcedure ##
### Input ###
| Property          | Type                              | Description                                             | Example                                   |
|-------------------|-----------------------------------|---------------------------------------------------------|-------------------------------------------|
| Execute           | string                            | The stored procedure that will be executed.             | `SpGetResultsByAge @Age` 
| Parameters        | Array{Name: string, Value: string} | A array of parameters to be appended to the query.     | `Name = Age, Value = 42`
| Connection String | string                            | Connection String to be used to connect to the database.| `Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;`

### Options ###
| Property               | Type                 | Description                                                |
|------------------------|----------------------|------------------------------------------------------------|
| Command Timeout        | int                  | Timeout in seconds to be used for the query. 60 seconds by default. |
| Sql Transaction Isolation Level | SqlTransationIsolationLevel | Transactions specify an isolation level that defines the degree to which one transaction must be isolated from resource or data modifications made by other transactions. Possible values are: Default, None, Serializable, ReadUncommitted, ReadCommitted, RepeatableRead, Snapshot. Additional documentation https://msdn.microsoft.com/en-us/library/ms378149(v=sql.110).aspx |

### Result ###
The result of the task will always be of type json Array.

Example result
```
#!json
[ 
 {
  "Name": "Foo",
  "Age": 42
 },
 {
  "Name": "Adam",
  "Age": 42
 }
]
```
```
#!text
The second name 'Adam' can be now be accessed by #result[1].Name in the process parameter editor.

```

## Sql.BulkInsert ##
### Input ###
| Property          | Type   | Description                                                                                                                                                                                                                                      | Example                                                                            |
|-------------------|--------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------|
| Input Data        | string | The data that will be inserted into the database. The data is a json string formated as Json Array of objects. All object property names need to match with the destination table column names.                                                  | `[{"Column1": "One", "Column2": 10},{"Column1": "Two", "Column2": 20}]`         |
| Table Name        | string | Destination table name.                                                                                                                                                                                                                          | MyTable                                                                            |
| Connection String | string | Connection String to be used to connect to the database.                                                                                                                                                                                         | Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword; |

 
### Options ###
| Property                         | Type                        | Description                                                                                                                                                                                                                                                                                                                                                       |
|----------------------------------|-----------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Command Timeout Seconds          | int                         | Timeout in seconds to be used for the query. Default is 60 seconds,                                                                                                                                                                                                                                                                                               |
| Fire Triggers                    | bool                        | When specified, cause the server to fire the insert triggers for the rows being inserted into the database.                                                                                                                                                                                                                                                       |
| Keep Identity                    | bool                        | Preserve source identity values. When not specified, identity values are assigned by the destination.                                                                                                                                                                                                                                                             |
| Sql Transaction Isolation Level  | SqlTransationIsolationLevel | Transactions specify an isolation level that defines the degree to which one transaction must be isolated from resource or data modifications made by other transactions. Possible values are: Default, None, Serializable, ReadUncommitted, ReadCommitted, RepeatableRead, Snapshot. Additional documentation https://msdn.microsoft.com/en-us/library/ms378149(v=sql.110).aspx |
| Convert Empty PropertyValues To Null | bool                    | If the input properties have empty values i.e. "", the values will be converted to null if this parameter is set to true.                                                                                                                                                                                                                                                     |

### Result ###
Integer - Number of copied rows

## Sql.BatchOperation ##
### Input ###
| Property          | Type                              | Description                                             | Example                                   |
|-------------------|-----------------------------------|---------------------------------------------------------|-------------------------------------------|
| Query             | string                            | The query that will be executed to the database.        | `insert into MyTable(ID,NAME) VALUES (@Id, @FirstName)` 
| Input Json        | string                            | A Json Array of objects that has their properties mapped to the parameters in the Query      | `[{"Id":10, "FirstName": "Foo"},{"Id":15, "FirstName": "Bar"}]`
| Connection String | string                            | Connection String to be used to connect to the database.| `Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;`


### Options ###
| Property               | Type                 | Description                                                |
|------------------------|----------------------|------------------------------------------------------------|
| Command Timeout Seconds       | int                  | Timeout in seconds to be used for the query. 60 seconds by default. |
| Sql Transaction Isolation Level | SqlTransationIsolationLevel | Transactions specify an isolation level that defines the degree to which one transaction must be isolated from resource or data modifications made by other transactions. Possible values are: Default, None, Serializable, ReadUncommitted, ReadCommitted, RepeatableRead, Snapshot. Additional documentation https://msdn.microsoft.com/en-us/library/ms378149(v=sql.110).aspx | 

### Result ###
Integer - Number of affected rows

###Example usage###
![BatchOperationExample.png](https://bitbucket.org/repo/qKzXjx/images/1917332460-BatchOperationExample.png)