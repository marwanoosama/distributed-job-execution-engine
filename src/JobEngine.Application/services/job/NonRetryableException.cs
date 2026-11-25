namespace JobEngine.Application.services.job;

public class NonRetryableException(string message) : Exception(message);
