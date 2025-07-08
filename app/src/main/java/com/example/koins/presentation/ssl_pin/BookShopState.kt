package com.example.koins.presentation.ssl_pin

import com.example.koins.domain.model.Books

data class BookShopState(
    val isLoading: Boolean = false,
    val error: String? = null,
    val books: Books? = null
)
