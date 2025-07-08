package com.example.koins.presentation.ssl_pin.component

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.wrapContentHeight
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.example.koins.presentation.ssl_pin.BookShopViewModel

@Composable
fun BookShopScreen(
    bookShopViewModel: BookShopViewModel = hiltViewModel()
) {

    val state by remember { bookShopViewModel.state }
    var text by remember { mutableStateOf("loading data...") }

    Box(modifier = Modifier.fillMaxSize()
        .background(color = Color.DarkGray)
    ){

        Column(
            modifier = Modifier.fillMaxWidth()
                .wrapContentHeight()
                .align(Alignment.Center),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Button(
                modifier = Modifier.padding(5.dp),
                onClick = { bookShopViewModel.getBooks() },
                colors = ButtonDefaults.buttonColors(
                    containerColor = Color.Green,
                    contentColor = Color.White
                )
            ) {
                Text(text = "Fetch Books", fontSize = 15.sp, color = Color.White)
            }

            Spacer(modifier = Modifier.height(20.dp))

            if (state.isLoading){
                text = "loading data..."
            }
            if (!state.error.isNullOrEmpty()){
                text = state.error.toString()
            }
            if(state.books != null){
                if (state.books!!.books.isNullOrEmpty()){
                    text = "No books found"
                } else {
                    text = state.books!!.books.toString()
                }
            }

            Text(text = text, fontSize = 15.sp, color = Color.White)
        }

    }

}