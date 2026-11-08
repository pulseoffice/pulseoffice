using PulseOffice.Data;
using Xunit;

namespace PulseOffice.Data.Tests;

public sealed class ReadOnlySqlPolicyTests
{
    private readonly ReadOnlySqlPolicy _policy = new();

    [Theory]
    [InlineData("SELECT * FROM clienti")]
    [InlineData("select nome, \"update\" from [clienti] where note = 'drop table x; --'")]
    [InlineData("WITH t AS (SELECT 1 AS a) SELECT a FROM t;")]
    [InlineData("SELECT updated_at, insert_count FROM log -- DELETE FROM log")]
    [InlineData("SELECT /* ; DROP TABLE x */ 1")]
    [InlineData("SELECT `nome` FROM clienti -- note")]
    [InlineData("VALUES (1), (2)")]
    public void Plain_reads_are_allowed(string sql) => Assert.True(_policy.IsReadOnly(sql));

    [Theory]
    [InlineData("DELETE FROM clienti")]
    [InlineData("SELECT 1; DROP TABLE clienti")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("WITH d AS (DELETE FROM clienti RETURNING *) SELECT * FROM d")]
    [InlineData("SELECT * INTO copia FROM clienti")]
    [InlineData("SELECT * FROM clienti INTO OUTFILE '/tmp/x'")]
    [InlineData("SELECT load_file('/etc/passwd')")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("EXEC xp_cmdshell 'dir'")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'x', 'SELECT 1')")]
    [InlineData("ATTACH DATABASE '/tmp/x.db' AS x")]
    [InlineData("PRAGMA writable_schema = 1")]
    [InlineData("SELECT 'unterminated")]
    [InlineData("SELECT 1 /* unterminated")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UPDATE clienti SET nome = 'x'")]
    [InlineData("SELECT '\\'; DROP TABLE clienti; --'")]
    [InlineData("SELECT 1 # 2; DROP TABLE clienti")]
    [InlineData("SELECT 1 --1; DROP TABLE clienti")]
    [InlineData("SELECT 1 /*! ; DROP TABLE clienti */")]
    [InlineData("SELECT ARRAY[1]; DROP TABLE clienti; --]")]
    [InlineData("SELECT $a$ ; DROP TABLE clienti; $a$")]
    [InlineData("SELECT setval('s', 1)")]
    [InlineData("SELECT query_to_xml('DELETE FROM clienti RETURNING id', true, false, '')")]
    [InlineData("SELECT lo_from_bytea(0, 'x')")]
    [InlineData("SELECT pg_file_write('/tmp/x', 'y', false)")]
    [InlineData("SELECT dbms_xmlgen.getxml('SELECT 1') FROM dual")]
    [InlineData("SELECT * FROM TABLE(dbms_sql.execute(1))")]
    [InlineData("SELECT pg_reload_conf()")]
    [InlineData("SELECT fts3_tokenizer('simple', x'00')")]
    [InlineData("SELECT \"pg_read_file\"('/etc/passwd')")]
    [InlineData("SELECT pg_catalog.\"lo_import\" ('/etc/passwd')")]
    [InlineData("SELECT U&\"pg\\005fread_file\"('/etc/passwd')")]
    [InlineData("SELECT \"load_extension\"/* x */('evil')")]
    [InlineData("SELECT [xp_cmdshell]('dir')")]
    [InlineData("SELECT `load_file`\n('/etc/passwd')")]
    public void Writes_and_escapes_are_refused(string sql) => Assert.False(_policy.IsReadOnly(sql));
}
