SET NOCOUNT ON;

DECLARE @OccurrenceKey nvarchar(200) = N'SQMS:OpportunityOpen:2030:Q1:2030-01-02';
IF (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationOccurrenceKey] = @OccurrenceKey) <> 1
    THROW 51000, 'Occurrence replay created a duplicate outbox row.', 1;

SELECT [NotificationOutboxId], [NotificationOccurrenceKey], [NotificationType], [Status], [RetryCount], [NotificationRecipientsJson]
FROM [dbo].[NotificationOutbox]
WHERE [NotificationOccurrenceKey] = @OccurrenceKey;
