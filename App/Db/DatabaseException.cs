namespace App.Db;

internal sealed class DatabaseException : Exception
{
    public DatabaseException(string message) : base(message)
    {
    }
}