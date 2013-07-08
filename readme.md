Basic command line utility capable of generate DTO based on SQL query.

Example:

	DTOGen -cn CS_NAME -name MyEntity -ns My.Namespace -sql "SELECT * FROM Entities"

It supposes that you added to the configuration file the connection string named "CS_NAME".
