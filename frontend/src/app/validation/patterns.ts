// The page's copy of the core's rules for an email and a one-time code (research R18), so it refuses to send what the
// API would refuse. ValidationPatternParityTests compares these three with Email.Pattern, OneTimeCodeValue.Pattern
// and Email.MaxLength; the shared examples in testdata/validation-examples.json run against both.

export const EMAIL_MAX_LENGTH = 254;

export const EMAIL_PATTERN = /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$/;

export const ONE_TIME_CODE_PATTERN = /^[0-9]{6}$/;
