CREATE TABLE IF NOT EXISTS accounts (
    account_id VARCHAR(64) NOT NULL PRIMARY KEY,
    email_normalized VARCHAR(254) NOT NULL UNIQUE,
    password_hash VARCHAR(512) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS account_sessions (
    token_hash BINARY(32) NOT NULL PRIMARY KEY,
    account_id VARCHAR(64) NOT NULL,
    expires_at DATETIME NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ix_account_sessions_account (account_id),
    CONSTRAINT fk_account_sessions_account FOREIGN KEY (account_id)
        REFERENCES accounts (account_id) ON DELETE CASCADE
);
