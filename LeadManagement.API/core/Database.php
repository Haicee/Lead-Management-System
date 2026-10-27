<?php
/**
 * Database Connection Singleton
 * 
 * Provides a single PDO connection instance throughout the application lifecycle.
 */

class Database
{
    private static ?PDO $instance = null;

    /**
     * Get the PDO connection instance.
     * Creates a new connection if one doesn't exist.
     */
    public static function getConnection(): PDO
    {
        if (self::$instance === null) {
            $config = require __DIR__ . '/../config/database.php';

            $dsn = sprintf(
                'mysql:host=%s;port=%d;dbname=%s;charset=%s',
                $config['host'],
                $config['port'],
                $config['database'],
                $config['charset']
            );

            self::$instance = new PDO($dsn, $config['username'], $config['password'], $config['options']);
        }

        return self::$instance;
    }

    /**
     * Close the connection.
     */
    public static function close(): void
    {
        self::$instance = null;
    }
}
