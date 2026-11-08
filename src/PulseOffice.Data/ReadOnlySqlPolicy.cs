namespace PulseOffice.Data;

public interface IReadOnlySqlPolicy
{
    bool IsReadOnly(string sql);
}

public sealed class ReadOnlySqlPolicy : IReadOnlySqlPolicy
{
    private readonly HashSet<string> _openers = new(StringComparer.Ordinal) { "SELECT", "WITH", "VALUES", "TABLE" };

    private readonly HashSet<string> _forbidden = new(StringComparer.Ordinal)
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "UPSERT", "DROP", "ALTER", "CREATE", "TRUNCATE", "RENAME", "GRANT", "REVOKE",
        "EXEC", "EXECUTE", "CALL", "DO", "COPY", "LOAD", "ATTACH", "DETACH", "PRAGMA", "VACUUM", "REINDEX", "ANALYZE",
        "LOCK", "UNLOCK", "SET", "RESET", "COMMIT", "ROLLBACK", "SAVEPOINT", "BEGIN", "DECLARE", "PREPARE", "DEALLOCATE",
        "INTO", "OUTFILE", "DUMPFILE", "HANDLER", "SHUTDOWN", "KILL", "LISTEN", "NOTIFY", "IMPORT", "BACKUP", "RESTORE",
        "XP_CMDSHELL", "SP_EXECUTESQL", "SP_OACREATE", "OPENROWSET", "OPENDATASOURCE", "OPENQUERY", "LOAD_FILE",
        "PG_READ_FILE", "PG_READ_BINARY_FILE", "PG_LS_DIR", "LO_IMPORT", "LO_EXPORT", "DBLINK", "DBLINK_EXEC",
        "UTL_HTTP", "UTL_FILE", "UTL_TCP", "DBMS_PIPE", "DBMS_SCHEDULER", "SYS_EXEC", "LOAD_EXTENSION", "WRITEFILE",
        "READFILE", "SETVAL", "NEXTVAL", "LO_UNLINK", "PG_TERMINATE_BACKEND", "PG_CANCEL_BACKEND", "DBLINK_CONNECT",
        "QUERY_TO_XML", "QUERY_TO_XMLSCHEMA", "QUERY_TO_XML_AND_XMLSCHEMA", "CURSOR_TO_XML", "LO_FROM_BYTEA", "LO_PUT",
        "LO_CREATE", "LO_CREAT", "LO_TRUNCATE", "PG_FILE_WRITE", "PG_FILE_UNLINK", "PG_FILE_RENAME", "PG_FILE_SYNC",
        "PG_RELOAD_CONF", "PG_ROTATE_LOGFILE", "PG_PROMOTE", "PG_SWITCH_WAL", "PG_CREATE_RESTORE_POINT",
        "PG_LOGICAL_EMIT_MESSAGE", "DBMS_SQL", "DBMS_XMLGEN", "DBMS_XMLQUERY", "DBMS_JAVA", "DBMS_LOB", "DBMS_AQ",
        "DBMS_JOB", "DBMS_LDAP", "HTTPURITYPE", "FTS3_TOKENIZER", "SYS_EVAL",
        SqlWordReader.QuotedCall,
    };

    private readonly IReadOnlyList<SqlLexicalRules> _dialects =
    [
        new(BackslashEscapes: false, HashComments: false, DashCommentNeedsSpace: false, DollarQuotes: true, BracketIdentifiers: false, BacktickIdentifiers: false),
        new(BackslashEscapes: true, HashComments: true, DashCommentNeedsSpace: true, DollarQuotes: false, BracketIdentifiers: false, BacktickIdentifiers: true),
        new(BackslashEscapes: false, HashComments: false, DashCommentNeedsSpace: false, DollarQuotes: false, BracketIdentifiers: true, BacktickIdentifiers: true),
    ];

    public bool IsReadOnly(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return _dialects.All(rules => IsReadOnly(new SqlWordReader(rules).Words(sql)));
    }

    private bool IsReadOnly(IReadOnlyList<string>? words)
    {
        if (words is not { Count: > 0 }) return false;
        if (!_openers.Contains(words[0])) return false;
        if (words.Any(_forbidden.Contains)) return false;

        var separators = words.Count(word => word == ";");
        return separators == 0 || (separators == 1 && words[^1] == ";");
    }
}
