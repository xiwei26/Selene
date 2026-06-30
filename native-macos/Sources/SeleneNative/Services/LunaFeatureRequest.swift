import Foundation

struct LunaFeatureRequest: Sendable {
    let serverURL: URL
    let cookie: String
    let session: URLSession

    func getJSON<T: Decodable>(path: String, queryItems: [URLQueryItem] = [], as type: T.Type) async throws -> T {
        let url = path
            .split(separator: "/")
            .reduce(serverURL) { partial, component in
                partial.appendingPathComponent(String(component))
            }
        var components = URLComponents(url: url, resolvingAgainstBaseURL: false)
        if !queryItems.isEmpty {
            components?.queryItems = queryItems
        }
        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url, timeoutInterval: 20)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if !cookie.isEmpty {
            request.setValue(cookie, forHTTPHeaderField: "Cookie")
        }

        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.responseError(statusCode: -1)
        }
        guard (200...299).contains(httpResponse.statusCode) else {
            if httpResponse.statusCode == 401 || httpResponse.statusCode == 403 {
                throw APIError.unauthorized
            }
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }

        do {
            return try JSONDecoder().decode(type, from: data)
        } catch {
            throw APIError.parseError
        }
    }
}

struct LunaDataResponse<T: Decodable>: Decodable {
    let data: T
}
