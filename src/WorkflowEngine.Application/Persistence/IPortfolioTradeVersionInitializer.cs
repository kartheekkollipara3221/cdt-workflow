namespace WorkflowEngine.Application.Persistence;

// Optional init-db extension: seed both PortfolioTrade versions and choose the active one.
public interface IPortfolioTradeVersionInitializer
{
    string Initialize(int activePortfolioTradeVersion);
}
