using Jamq.Client.Rabbit.Consuming;
using Jamq.Client.Rabbit.Producing;
using RabbitMQ.Client;

namespace Jamq.Client.Rabbit;

internal static class Ensure
{
    public static void Produce(IModel channel, RabbitProducerParameters parameters)
    {
        channel.ExchangeDeclare(
            parameters.ExchangeName,
            parameters.ExchangeType.ToString().ToLower(),
            true);
    }

    public static QueueDeclareOk Consume(IModel channel, RabbitConsumerParameters parameters)
    {
        if (parameters.ExchangeName is not null)
        {
            channel.ExchangeDeclare(
                exchange: parameters.ExchangeName,
                type: parameters.ExchangeType.ToString().ToLower(),
                durable: true,
                arguments: parameters.AdditionalExchangeArguments ?? new Dictionary<string, object>(0));
        }

        var queueArguments = new Dictionary<string, object>(
            parameters.AdditionalQueueArguments ?? new Dictionary<string, object>(0));

        var queueName = parameters.Exclusive
            ? parameters.QueueName.WithRandomSuffix()
            : parameters.QueueName;
        parameters.DeclaredQueueName = queueName;

        if (parameters.DeadLetterExchange is not null)
        {
            var exchangeName = parameters.DeadLetterExchange.ExchangeName
                ?? throw new ArgumentNullException(
                    nameof(parameters.DeadLetterExchange.ExchangeName),
                    "The exchange name for the dead letter exchange can't be null");

            Consume(channel, parameters.DeadLetterExchange);

            queueArguments.Add("x-dead-letter-exchange", exchangeName);
            queueArguments.Add("x-dead-letter-routing-key", queueName);
        }

        var result = channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: parameters.Exclusive,
            autoDelete: false,
            arguments: queueArguments);

        if (parameters is { RoutingKeys: not null, ExchangeName: not null })
        {
            foreach (var routingKey in parameters.RoutingKeys)
            {
                channel.QueueBind(queueName, parameters.ExchangeName, routingKey);
            }
        }

        if (parameters.ProcessingOrder != ProcessingOrder.Unmanaged)
        {
            channel.BasicQos(0, parameters.ProcessingOrder.Value, true);
        }

        return result;
    }
}