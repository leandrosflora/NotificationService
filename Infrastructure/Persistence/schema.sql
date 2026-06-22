CREATE TABLE notifications (
    id UUID PRIMARY KEY,
    source_event_id UUID NOT NULL,
    recipient_id UUID NOT NULL,
    type VARCHAR(80) NOT NULL,
    priority VARCHAR(30) NOT NULL,
    status VARCHAR(30) NOT NULL,
    locale VARCHAR(20) NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_notifications_source_event UNIQUE (source_event_id)
);

CREATE TABLE notification_deliveries (
    id UUID PRIMARY KEY,
    notification_id UUID NOT NULL REFERENCES notifications(id),
    channel VARCHAR(30) NOT NULL,
    status VARCHAR(30) NOT NULL,
    destination TEXT NULL,
    template_id UUID NULL,
    template_version INTEGER NULL,
    subject TEXT NULL,
    body TEXT NULL,
    provider_message_id VARCHAR(300) NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    not_before TIMESTAMPTZ NULL,
    next_attempt_at TIMESTAMPTZ NULL,
    processing_token UUID NULL,
    processing_lease_until TIMESTAMPTZ NULL,
    last_error VARCHAR(1000) NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    accepted_at TIMESTAMPTZ NULL,
    delivered_at TIMESTAMPTZ NULL
);

CREATE INDEX idx_notification_deliveries_dispatch ON notification_deliveries (status, not_before, next_attempt_at, processing_lease_until);
CREATE INDEX idx_notification_deliveries_provider ON notification_deliveries (provider_message_id);

CREATE TABLE notification_templates (
    id UUID PRIMARY KEY,
    type VARCHAR(80) NOT NULL,
    channel VARCHAR(30) NOT NULL,
    locale VARCHAR(20) NOT NULL,
    version INTEGER NOT NULL,
    subject_template TEXT NULL,
    body_template TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_notification_templates_key UNIQUE (type, channel, locale, version)
);

CREATE TABLE notification_preferences (
    id UUID PRIMARY KEY,
    recipient_id UUID NOT NULL,
    notification_type VARCHAR(80) NOT NULL,
    channel VARCHAR(30) NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_notification_preferences_key UNIQUE (recipient_id, notification_type, channel)
);

CREATE TABLE recipient_contacts (
    recipient_id UUID PRIMARY KEY,
    locale VARCHAR(20) NULL,
    email TEXT NULL,
    phone_number TEXT NULL,
    push_token TEXT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE inbox_messages (
    message_id UUID PRIMARY KEY,
    message_type VARCHAR(200) NULL,
    processed_at TIMESTAMPTZ NULL
);

CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    topic VARCHAR(200) NULL,
    message_type VARCHAR(200) NULL,
    aggregate_key VARCHAR(100) NULL,
    payload JSONB NULL,
    created_at TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    next_attempt_at TIMESTAMPTZ NULL,
    last_error TEXT NULL
);

CREATE INDEX idx_outbox_messages_dispatch ON outbox_messages (processed_at, next_attempt_at, created_at);
