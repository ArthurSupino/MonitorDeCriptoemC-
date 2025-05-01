using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static readonly List<string> criptomoedas = new List<string>
    {
        "BTC",  // Bitcoin
        "ETH",  // Ethereum
        "LTC",  // Litecoin
        "BCH",  // Bitcoin Cash
        "XRP",  // Ripple
        "ADA",  // Cardano
        "DOT",  // Polkadot
        "LINK", // Chainlink
        "XLM",  // Stellar
        "DOGE"  // Dogecoin
    };

    private static readonly ConcurrentDictionary<string, decimal> precosAtuais = new ConcurrentDictionary<string, decimal>();
    private static readonly ConcurrentDictionary<string, decimal> precosAnteriores = new ConcurrentDictionary<string, decimal>();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Monitor de Criptomoedas - Pressione ESC para sair");
        Console.WriteLine("-----------------------------------------------");

        using var cts = new CancellationTokenSource();
        var monitorTeclaEsc = MonitorarTeclaEscAsync(cts);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await AtualizarCotacoesAsync(cts.Token);
                await Task.Delay(30000, cts.Token); // Aguarda 30 segundos
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar cotações: {ex.Message}");
                await Task.Delay(5000, cts.Token); // Aguarda 5 segundos em caso de erro
            }
        }

        await monitorTeclaEsc;
        Console.WriteLine("\nPrograma encerrado.");
    }

    static HttpClient CriarClienteHttp()
    {
        var cliente = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        cliente.DefaultRequestHeaders.Add("User-Agent", "MonitorCripto/1.0");
        cliente.DefaultRequestHeaders.Add("Accept", "application/json");
        return cliente;
    }

    static async Task ObterEConverterCotacaoAsync(string simbolo, CancellationToken token)
    {
        var clienteHttp = CriarClienteHttp();
        var urlRequisicao = $"https://api.exchange.cryptomkt.com/api/3/public/price/rate?from={simbolo}&to=USDT";
        
        var resposta = await clienteHttp.GetAsync(urlRequisicao, token);
        resposta.EnsureSuccessStatusCode();

        var json = await resposta.Content.ReadAsStringAsync(token);

        using var documento = JsonDocument.Parse(json);
        if (documento.RootElement.TryGetProperty(simbolo, out var dadosMoeda))
        {
            var precoString = dadosMoeda.GetProperty("price").GetString();
            decimal precoAtual = decimal.Parse(precoString, CultureInfo.InvariantCulture);

            // Atualiza os preços
            if (precosAtuais.TryGetValue(simbolo, out var precoAnterior))
            {
                precosAnteriores[simbolo] = precoAnterior;
            }
            precosAtuais[simbolo] = precoAtual;

            // Exibe o resultado
            ExibirResultadosNoConsole(simbolo, precoAtual, precosAnteriores.GetValueOrDefault(simbolo, precoAtual));
        }
    }

    static async Task AtualizarCotacoesAsync(CancellationToken token)
    {
        Console.Clear();
        Console.WriteLine($"Última atualização: {DateTime.Now:HH:mm:ss}");
        Console.WriteLine("-----------------------------------------------");

        var tasks = criptomoedas.Select(simbolo => ObterEConverterCotacaoAsync(simbolo, token));
        await Task.WhenAll(tasks);
    }

    static void ExibirResultadosNoConsole(string simbolo, decimal precoAtual, decimal precoAnterior)
    {
        var corOriginal = Console.ForegroundColor;
        Console.ForegroundColor = precoAtual > precoAnterior ? ConsoleColor.Green : ConsoleColor.Red;
        var variacao = precoAtual > precoAnterior ? "↑" : "↓";
        Console.WriteLine($"{simbolo}: ${precoAtual:N2} {variacao}");
        Console.ForegroundColor = corOriginal;
    }

    static async Task MonitorarTeclaEscAsync(CancellationTokenSource cts)
    {
        while (true)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                cts.Cancel();
                break;
            }
            await Task.Delay(100);
        }
    }
} 