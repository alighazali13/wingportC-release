namespace GamePort.Cashier.Application.Interfaces;

public interface ISecretHasher
{
    string Hash(string secret);
    bool Verify(string secret, string hash);
}
