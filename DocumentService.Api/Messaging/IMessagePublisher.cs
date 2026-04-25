using System.Threading.Tasks;

namespace DocumentProcessing.Api.Messaging
{
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(T message);
    }
}
