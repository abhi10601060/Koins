package com.example.koins.presentation.ssl_pin

import androidx.compose.runtime.State
import androidx.compose.runtime.mutableStateOf
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.koins.common.Resource
import com.example.koins.domain.use_case.get_books.GetBooksUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.onEach
import javax.inject.Inject

@HiltViewModel
class BookShopViewModel @Inject constructor(
    private val getBooksUseCase: GetBooksUseCase
) : ViewModel() {

    private val _state = mutableStateOf(BookShopState())
    val state : State<BookShopState> = _state

    fun getBooks(){
        getBooksUseCase.invoke().onEach { res ->
            when(res){
                is Resource.Success -> {
                    _state.value = BookShopState(books = res.data!!)
                }
                is Resource.Error -> {
                    _state.value = BookShopState(error = res.message ?: "Internal server error")
                }
                is Resource.Loading -> {
                    _state.value = BookShopState(isLoading = true)
                }
                else -> {}
            }
        }.launchIn(viewModelScope)
    }

}