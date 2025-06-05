// --- Metadata cho Plugin ---
// @ts-nocheck /* Bỏ qua kiểm tra kiểu TypeScript nếu bạn không dùng */
// name = Thư Viện HD Phim Lẻ
// type = movie
// version = 1.2 // Cập nhật phiên bản nếu có thay đổi trong tương lai
// logo = URL_DEN_LOGO_PLUGIN_CUA_BAN (Tùy chọn, ví dụ: https://your-domain.com/thuvienhd_logo.png)
// description = Duyệt phim lẻ từ Thư Viện HD (JSON).

// --- URL đến file JSON chứa dữ liệu phim của bạn ---
const JSON_DATA_URL = "https://a-z.azdata.workers.dev/kodi_thuvienhd_phim-le.json"; // ĐÃ CẬP NHẬT!

let cachedMovies = null; // Biến để lưu trữ dữ liệu phim đã tải

// Hàm tải và cache dữ liệu phim
async function fetchAndCacheMovies() {
    if (cachedMovies) {
        return cachedMovies;
    }
    try {
        // Thêm một tham số ngẫu nhiên để cố gắng tránh cache phía server/CDN nếu cần thiết
        const response = await fetch(JSON_DATA_URL + "?t=" + Date.now());
        if (!response.ok) {
            console.error(`Lỗi HTTP khi tải JSON: ${response.status} ${response.statusText}`);
            // Thông báo lỗi cho người dùng trong CloudStream
            showToast(`Lỗi tải dữ liệu phim: ${response.status}`, true);
            return [];
        }
        cachedMovies = await response.json();
        if (!Array.isArray(cachedMovies)) {
            console.error("Dữ liệu JSON không phải là một mảng.");
            showToast("Lỗi: Định dạng dữ liệu phim không đúng.", true);
            cachedMovies = []; // Đặt lại thành mảng rỗng để tránh lỗi sau này
            return [];
        }
        return cachedMovies;
    } catch (error) {
        console.error("Lỗi khi tải hoặc phân tích JSON:", error);
        showToast(`Lỗi nghiêm trọng khi xử lý dữ liệu: ${error.message}`, true);
        cachedMovies = []; // Đặt lại để tránh lỗi lặp lại
        return [];
    }
}

// Hàm chính để tải danh sách phim (cho trang chủ và tìm kiếm)
async function search(query) {
    const moviesData = await fetchAndCacheMovies();
    if (!moviesData || moviesData.length === 0) {
        // Thử tải lại một lần nếu không có dữ liệu, có thể do cache rỗng ban đầu
        cachedMovies = null; // Xóa cache để thử lại
        const refreshedMoviesData = await fetchAndCacheMovies();
        if (!refreshedMoviesData || refreshedMoviesData.length === 0) {
            return [];
        }
        // Nếu tải lại thành công, gán lại moviesData
        // moviesData = refreshedMoviesData; // Dòng này không cần thiết vì filteredMovies sẽ dùng refreshedMoviesData
    }


    let filteredMovies = cachedMovies; // Luôn dùng cachedMovies đã được cập nhật (hoặc tải mới)

    // Xử lý tìm kiếm đơn giản (có thể cải thiện)
    if (query) {
        const lowerQuery = query.toLowerCase();
        filteredMovies = cachedMovies.filter(movie =>
            movie.name && movie.name.toLowerCase().includes(lowerQuery)
        );
    }

    return filteredMovies.map(movie => ({
        title: movie.name,
        url: movie.name, // Sử dụng tên phim làm định danh tạm thời để truyền cho loadLinks
        poster: movie.poster_url,
        description: movie.description,
        // Bạn có thể thêm các trường khác nếu CloudStream hỗ trợ
        // ví dụ: year, tags (nếu có trong JSON)
    }));
}

// Hàm để tải các liên kết (chất lượng) của một bộ phim cụ thể
async function loadLinks(movieIdentifier) { // movieIdentifier ở đây là movie.name
    // Không cần gọi fetchAndCacheMovies() nữa nếu bạn chắc chắn search() đã chạy và cache dữ liệu
    // Tuy nhiên, để an toàn, có thể gọi lại nếu cachedMovies rỗng
    let moviesToSearch = cachedMovies;
    if (!moviesToSearch) {
        moviesToSearch = await fetchAndCacheMovies();
    }

    if (!moviesToSearch) {
        return [];
    }

    const selectedMovie = moviesToSearch.find(movie => movie.name === movieIdentifier);

    if (!selectedMovie || !selectedMovie.links || selectedMovie.links.length === 0) {
        showToast("Không tìm thấy link cho phim này.", true);
        return [];
    }

    return selectedMovie.links.map(link => {
        let qualityName = link.quality || "Chất lượng mặc định";
        // if (link.file_name) { // Tên file có thể quá dài, chỉ dùng quality
        //     qualityName += ` (${link.file_name})`;
        // }

        // Cố gắng xác định chất lượng số từ file_name (đơn giản)
        let numericQuality = 0;
        if (link.file_name) {
            if (link.file_name.toLowerCase().includes("2160p")) numericQuality = 2160;
            else if (link.file_name.toLowerCase().includes("1080p")) numericQuality = 1080;
            else if (link.file_name.toLowerCase().includes("720p")) numericQuality = 720;
            else if (link.file_name.toLowerCase().includes("480p")) numericQuality = 480;
        }


        return {
            name: qualityName, // Tên hiển thị của link (ví dụ: "7.77 GB")
            url: link.url,     // URL trực tiếp đến file video
            quality: numericQuality > 0 ? numericQuality : undefined, // Chất lượng số nếu có
            isDirect: true     // Quan trọng: Đánh dấu đây là link trực tiếp
        };
    });
}

// (Tùy chọn) Hàm load để tải thông tin chi tiết hơn khi người dùng vào trang chi tiết phim
async function load(movieIdentifier) {
    let moviesToSearch = cachedMovies;
    if (!moviesToSearch) {
        moviesToSearch = await fetchAndCacheMovies();
    }

    if (!moviesToSearch) {
        return null;
    }
    const movie = moviesToSearch.find(m => m.name === movieIdentifier);
    if (movie) {
        return {
            title: movie.name,
            url: movie.name, // Giữ nguyên định danh
            poster: movie.poster_url,
            description: movie.description,
            // Thêm các trường khác nếu cần hiển thị chi tiết hơn
        };
    }
    return null;
}

function showToast(message, isError = false) {
    // Đây là một hàm giả định, CloudStream có API riêng cho việc này, ví dụ:
    // CS.showToast(message, isError);
    console.log(`Toast: ${message} (Error: ${isError})`);
    // Trong CloudStream, bạn sẽ gọi API này nếu nó tồn tại và được phép trong plugin JS
}